using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Features;
using TomodachiDrawer.Core.Models;
using TomodachiDrawerCn.Api.Generation;
using TomodachiDrawerCn.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 8 * 1024 * 1024;
});

builder.Services.AddCors();

var app = builder.Build();

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

var dataRoot = Environment.GetEnvironmentVariable("TOMODACHI_DATA_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "data");
var uploadDir = Path.Combine(dataRoot, "uploads");
var outputDir = Path.Combine(dataRoot, "outputs");
var previewDir = Path.Combine(dataRoot, "previews");
Directory.CreateDirectory(uploadDir);
Directory.CreateDirectory(outputDir);
Directory.CreateDirectory(previewDir);

var jobs = new ConcurrentDictionary<string, JobRecord>();
var queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
{
    SingleReader = true,
    SingleWriter = false
});
var queueLock = new object();
string? runningJobUuid = null;
var transientJobRetention = TimeSpan.FromMinutes(30);
CleanupDirectoryFiles(uploadDir, transientJobRetention, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
CleanupDirectoryFiles(outputDir, transientJobRetention, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
CleanupDirectoryFiles(previewDir, transientJobRetention, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

_ = Task.Run(async () =>
{
    await foreach (var jobUuid in queue.Reader.ReadAllAsync())
    {
        if (!jobs.TryGetValue(jobUuid, out var record))
        {
            continue;
        }

        lock (queueLock)
        {
            runningJobUuid = jobUuid;
            record.Status = JobStatuses.Running;
            record.QueuePosition = 1;
            record.QueueAhead = 0;
            record.ProgressPercent = 8;
            record.StartedAt = DateTimeOffset.UtcNow;
            record.Message = "正在生成绘画文件。";
            RefreshQueuePositions(jobs, runningJobUuid);
        }

        try
        {
            var generated = await DrawingGenerator.GenerateAsync(
                new GenerateDrawingRequest(
                    record.SourceImagePath,
                    record.OutputPath,
                    record.BoardType,
                    record.OutputType,
                    record.ColourMatcher,
                    record.TspTimeLimit,
                    record.SwitchVersion,
                    record.PreviewPath,
                    record.CropX,
                    record.CropY,
                    record.CropSize,
                    progress =>
                    {
                        record.ProgressPercent = Math.Max(record.ProgressPercent, progress.Percent);
                        record.Message = progress.Message;
                    }
                )
            );

            record.OutputType = generated.OutputType;
            record.Status = JobStatuses.Success;
            record.ProgressPercent = 100;
            record.QueuePosition = 0;
            record.QueueAhead = 0;
            record.CompletedAt = DateTimeOffset.UtcNow;
            record.Message =
                $"Generated with TomodachiDrawer.Core. TDLD={generated.TdldBytes} bytes, output={generated.OutputBytes} bytes, estimated draw time={generated.EstimatedDrawTime.TotalSeconds:F1}s.";
        }
        catch (Exception ex)
        {
            record.Status = JobStatuses.Failed;
            record.ProgressPercent = 100;
            record.QueuePosition = 0;
            record.QueueAhead = 0;
            record.CompletedAt = DateTimeOffset.UtcNow;
            record.Message = ex is InvalidDataException or ArgumentException
                ? ex.Message
                : "生成失败，请稍后重试。";
        }
        finally
        {
            TryDeleteFile(record.SourceImagePath);

            lock (queueLock)
            {
                if (runningJobUuid == jobUuid)
                {
                    runningJobUuid = null;
                }

                RefreshQueuePositions(jobs, runningJobUuid);
            }
        }
    }
});

_ = Task.Run(async () =>
{
    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
    while (await timer.WaitForNextTickAsync())
    {
        CleanupTransientJobs(jobs, transientJobRetention);
        var knownJobIds = jobs.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        CleanupDirectoryFiles(uploadDir, transientJobRetention, knownJobIds);
        CleanupDirectoryFiles(outputDir, transientJobRetention, knownJobIds);
        CleanupDirectoryFiles(previewDir, transientJobRetention, knownJobIds);
    }
});

app.MapGet("/api/health", () =>
    Results.Ok(new HealthResponse("ok", "tomodachi-cn-api", DateTimeOffset.UtcNow, "0.1.0"))
);

app.MapGet("/api/jobs", () =>
{
    RefreshQueuePositions(jobs, runningJobUuid);
    return Results.Ok(
        jobs.Values
            .Where(job => job.Status == JobStatuses.Pending || job.Status == JobStatuses.Running)
            .OrderByDescending(job => job.CreatedAt)
            .Take(50)
            .Select(ToResponse)
            .ToArray()
    );
});

app.MapPost("/api/jobs", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("multipart/form-data is required.");
    }

    var form = await request.ReadFormAsync();
    var image = form.Files.GetFile("image");
    if (image is null || image.Length == 0)
    {
        return Results.BadRequest("image is required.");
    }

    if (image.Length > 8 * 1024 * 1024)
    {
        return Results.BadRequest("image must be 8MB or smaller.");
    }

    var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    if (!allowedTypes.Contains(image.ContentType))
    {
        return Results.BadRequest("only PNG, JPG, and WEBP images are supported.");
    }

    var jobUuid = Guid.NewGuid().ToString("N");
    var safeExtension = Path.GetExtension(image.FileName).ToLowerInvariant();
    if (safeExtension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
    {
        safeExtension = ".img";
    }

    var storedImagePath = Path.Combine(uploadDir, $"{jobUuid}{safeExtension}");
    await using (var stream = File.Create(storedImagePath))
    {
        await image.CopyToAsync(stream);
    }

    var boardType = NormalizeBoardType(GetFormValue(form, "boardType", "rp2040"));
    var requestedOutputType = GetFormValue(form, "outputType", "tdld").ToLowerInvariant();
    var outputType = requestedOutputType == "uf2" && DrawingGenerator.IsUf2Supported(boardType)
        ? "uf2"
        : "tdld";
    var outputPath = Path.Combine(outputDir, $"{jobUuid}.{outputType}");
    var previewPath = Path.Combine(previewDir, $"{jobUuid}.png");
    var switchVersion = ParseSwitchVersion(GetFormValue(form, "switchVersion", "switch2"));
    var tspTimeLimit = int.TryParse(GetFormValue(form, "tspTimeLimit", "30"), out var parsedTspTimeLimit)
        ? parsedTspTimeLimit
        : 30;
    var colourMatcher = GetFormValue(form, "colourMatcher", "arbitrary");
    var cropX = ParseDoubleFormValue(form, "cropX");
    var cropY = ParseDoubleFormValue(form, "cropY");
    var cropSize = ParseDoubleFormValue(form, "cropSize");

    var record = new JobRecord(
        jobUuid,
        Path.GetFileName(image.FileName),
        boardType,
        outputType,
        colourMatcher,
        tspTimeLimit,
        switchVersion,
        JobStatuses.Pending,
        1,
        0,
        1,
        DateTimeOffset.UtcNow,
        null,
        null,
        storedImagePath,
        outputPath,
        previewPath,
        cropX,
        cropY,
        cropSize,
        "任务已进入队列。"
    );

    jobs[jobUuid] = record;
    lock (queueLock)
    {
        RefreshQueuePositions(jobs, runningJobUuid);
    }

    await queue.Writer.WriteAsync(jobUuid);
    RefreshQueuePositions(jobs, runningJobUuid);
    return Results.Accepted($"/api/jobs/{record.JobUuid}", ToResponse(record));
});

app.MapGet("/api/jobs/{jobUuid}", (string jobUuid) =>
{
    RefreshQueuePositions(jobs, runningJobUuid);
    return jobs.TryGetValue(jobUuid, out var record)
        ? Results.Ok(ToResponse(record))
        : Results.NotFound();
});

app.MapGet("/api/jobs/{jobUuid}/download", (string jobUuid) =>
{
    if (
        !jobs.TryGetValue(jobUuid, out var record)
        || record.Status != JobStatuses.Success
        || !File.Exists(record.OutputPath)
    )
    {
        return Results.NotFound();
    }

    var contentType = record.OutputType == "uf2" ? "application/octet-stream" : "application/vnd.tomodachi.tdld";
    return Results.File(record.OutputPath, contentType, $"{record.JobUuid}.{record.OutputType}");
});

app.MapGet("/api/jobs/{jobUuid}/preview", (string jobUuid) =>
{
    if (
        !jobs.TryGetValue(jobUuid, out var record)
        || record.Status == JobStatuses.Pending
        || !File.Exists(record.PreviewPath)
    )
    {
        return Results.NotFound();
    }

    return Results.File(record.PreviewPath, "image/png", $"{record.JobUuid}.png");
});

app.Run();

static string GetFormValue(IFormCollection form, string key, string fallback)
{
    return form.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value.ToString()
        : fallback;
}

static double? ParseDoubleFormValue(IFormCollection form, string key)
{
    return form.TryGetValue(key, out var value)
        && double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
        ? parsed
        : null;
}

static string NormalizeBoardType(string boardType)
{
    return boardType.ToLowerInvariant() switch
    {
        "rp2350" => "rp2350",
        "esp32-s3" or "esp32s3" => "esp32-s3",
        _ => "rp2040",
    };
}

static SwitchVersion ParseSwitchVersion(string switchVersion)
{
    return switchVersion.ToLowerInvariant() switch
    {
        "switch1" => SwitchVersion.Switch1,
        _ => SwitchVersion.Switch2,
    };
}

static JobResponse ToResponse(JobRecord record) =>
    new(
        record.JobUuid,
        record.FileName,
        record.BoardType,
        record.OutputType,
        record.ColourMatcher,
        record.TspTimeLimit,
        record.Status,
        record.QueuePosition,
        record.QueueAhead,
        record.ProgressPercent,
        record.CreatedAt,
        record.StartedAt,
        record.CompletedAt,
        record.Status == JobStatuses.Success ? $"/api/jobs/{record.JobUuid}/download" : null,
        File.Exists(record.PreviewPath) ? $"/api/jobs/{record.JobUuid}/preview" : null,
        record.Message
    );

static void CleanupTransientJobs(ConcurrentDictionary<string, JobRecord> jobs, TimeSpan retention)
{
    var now = DateTimeOffset.UtcNow;
    foreach (var record in jobs.Values)
    {
        if (record.Status == JobStatuses.Pending || record.Status == JobStatuses.Running)
        {
            continue;
        }

        var completedAt = record.CompletedAt ?? record.CreatedAt;
        if (now - completedAt < retention)
        {
            continue;
        }

        if (!jobs.TryRemove(record.JobUuid, out _))
        {
            continue;
        }

        TryDeleteFile(record.SourceImagePath);
        TryDeleteFile(record.OutputPath);
        TryDeleteFile(Path.ChangeExtension(record.OutputPath, ".tdld"));
        TryDeleteFile(record.PreviewPath);
    }
}

static void CleanupDirectoryFiles(string directory, TimeSpan retention, IReadOnlySet<string> knownJobIds)
{
    var cutoff = DateTimeOffset.UtcNow - retention;
    foreach (var path in Directory.EnumerateFiles(directory))
    {
        try
        {
            if (knownJobIds.Contains(Path.GetFileNameWithoutExtension(path)))
            {
                continue;
            }

            var lastWriteTime = File.GetLastWriteTimeUtc(path);
            if (lastWriteTime < cutoff)
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; failed deletes will be retried by the next cleanup pass.
        }
    }
}

static void TryDeleteFile(string path)
{
    try
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    catch
    {
        // Best-effort cleanup; failed deletes will be retried by the next cleanup pass.
    }
}

static void RefreshQueuePositions(ConcurrentDictionary<string, JobRecord> jobs, string? runningJobUuid)
{
    var activeJobs = jobs.Values
        .Where(job => job.Status == JobStatuses.Pending || job.Status == JobStatuses.Running)
        .OrderBy(job => job.CreatedAt)
        .ToArray();

    var position = 1;
    foreach (var job in activeJobs)
    {
        if (job.JobUuid == runningJobUuid || job.Status == JobStatuses.Running)
        {
            job.QueuePosition = 1;
            job.QueueAhead = 0;
            job.ProgressPercent = Math.Max(job.ProgressPercent, 8);
            position++;
            continue;
        }

        job.QueuePosition = position;
        job.QueueAhead = Math.Max(0, position - 1);
        job.ProgressPercent = Math.Min(job.ProgressPercent, 5);
        position++;
    }
}

internal sealed class JobRecord(
    string JobUuid,
    string FileName,
    string BoardType,
    string OutputType,
    string ColourMatcher,
    int TspTimeLimit,
    SwitchVersion SwitchVersion,
    string Status,
    int QueuePosition,
    int QueueAhead,
    int ProgressPercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string SourceImagePath,
    string OutputPath,
    string PreviewPath,
    double? CropX,
    double? CropY,
    double? CropSize,
    string Message
)
{
    public string JobUuid { get; } = JobUuid;
    public string FileName { get; } = FileName;
    public string BoardType { get; } = BoardType;
    public string OutputType { get; set; } = OutputType;
    public string ColourMatcher { get; } = ColourMatcher;
    public int TspTimeLimit { get; } = TspTimeLimit;
    public SwitchVersion SwitchVersion { get; } = SwitchVersion;
    public string Status { get; set; } = Status;
    public int QueuePosition { get; set; } = QueuePosition;
    public int QueueAhead { get; set; } = QueueAhead;
    public int ProgressPercent { get; set; } = ProgressPercent;
    public DateTimeOffset CreatedAt { get; } = CreatedAt;
    public DateTimeOffset? StartedAt { get; set; } = StartedAt;
    public DateTimeOffset? CompletedAt { get; set; } = CompletedAt;
    public string SourceImagePath { get; } = SourceImagePath;
    public string OutputPath { get; } = OutputPath;
    public string PreviewPath { get; } = PreviewPath;
    public double? CropX { get; } = CropX;
    public double? CropY { get; } = CropY;
    public double? CropSize { get; } = CropSize;
    public string Message { get; set; } = Message;
}
