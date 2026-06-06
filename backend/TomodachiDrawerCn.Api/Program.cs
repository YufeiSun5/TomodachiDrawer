using System.Collections.Concurrent;
using System.Text.Json;
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
var galleryDir = Path.Combine(dataRoot, "gallery");
var galleryPreviewDir = Path.Combine(galleryDir, "previews");
var galleryOutputDir = Path.Combine(galleryDir, "outputs");
var galleryIndexPath = Path.Combine(galleryDir, "gallery.json");
Directory.CreateDirectory(uploadDir);
Directory.CreateDirectory(outputDir);
Directory.CreateDirectory(previewDir);
Directory.CreateDirectory(galleryPreviewDir);
Directory.CreateDirectory(galleryOutputDir);

var jobs = new ConcurrentDictionary<string, JobRecord>();
var galleryLock = new object();
var gallery = LoadGallery(galleryIndexPath);
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

app.MapGet("/api/jobs", (string? clientId) =>
{
    RefreshQueuePositions(jobs, runningJobUuid);
    var normalizedClientId = NormalizeClientId(clientId);
    var activeJobs = jobs.Values
        .Where(job => job.Status == JobStatuses.Pending || job.Status == JobStatuses.Running)
        .OrderBy(job => job.CreatedAt)
        .ToArray();
    var anonymousIndex = 1;

    return Results.Ok(
        activeJobs
            .Select(job =>
            {
                var isOwner = IsOwner(job, normalizedClientId);
                return ToJobResponse(job, isOwner, isOwner ? null : anonymousIndex++);
            })
            .Take(50)
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

    var clientId = NormalizeClientId(GetFormValue(form, "clientId", ""));
    if (string.IsNullOrWhiteSpace(clientId))
    {
        return Results.BadRequest("clientId is required.");
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
        clientId,
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
    return Results.Accepted($"/api/jobs/{record.JobUuid}", ToOwnerJobResponse(record));
});

app.MapGet("/api/jobs/{jobUuid}", (string jobUuid, string? clientId) =>
{
    RefreshQueuePositions(jobs, runningJobUuid);
    var normalizedClientId = NormalizeClientId(clientId);
    return jobs.TryGetValue(jobUuid, out var record)
        ? Results.Ok(ToJobResponse(record, IsOwner(record, normalizedClientId), null))
        : Results.NotFound();
});

app.MapGet("/api/jobs/{jobUuid}/download", (string jobUuid, string? clientId) =>
{
    var normalizedClientId = NormalizeClientId(clientId);
    if (
        !jobs.TryGetValue(jobUuid, out var record)
        || !IsOwner(record, normalizedClientId)
        || record.Status != JobStatuses.Success
        || !File.Exists(record.OutputPath)
    )
    {
        return Results.NotFound();
    }

    var contentType = record.OutputType == "uf2" ? "application/octet-stream" : "application/vnd.tomodachi.tdld";
    return Results.File(record.OutputPath, contentType, $"{record.JobUuid}.{record.OutputType}");
});

app.MapGet("/api/jobs/{jobUuid}/preview", (string jobUuid, string? clientId) =>
{
    var normalizedClientId = NormalizeClientId(clientId);
    if (
        !jobs.TryGetValue(jobUuid, out var record)
        || !IsOwner(record, normalizedClientId)
        || record.Status == JobStatuses.Pending
        || !File.Exists(record.PreviewPath)
    )
    {
        return Results.NotFound();
    }

    return Results.File(record.PreviewPath, "image/png", $"{record.JobUuid}.png");
});

app.MapGet("/api/gallery", (string? boardType, string? q, string? sort) =>
{
    lock (galleryLock)
    {
        var normalizedBoardType = NormalizeGalleryBoardFilter(boardType);
        var query = q?.Trim();
        var items = gallery
            .Where(item => normalizedBoardType == "all" || item.BoardType == normalizedBoardType)
            .Where(item => string.IsNullOrWhiteSpace(query) || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        items = sort?.ToLowerInvariant() switch
        {
            "popular" => items.OrderByDescending(item => item.Likes).ThenByDescending(item => item.CreatedAt).ToArray(),
            _ => items.OrderByDescending(item => item.CreatedAt).ToArray(),
        };

        return Results.Ok(items.Select(ToGalleryResponse).ToArray());
    }
});

app.MapPost("/api/gallery", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("multipart/form-data is required.");
    }

    var form = await request.ReadFormAsync();
    var jobUuid = GetFormValue(form, "jobUuid", "");
    var clientId = NormalizeClientId(GetFormValue(form, "clientId", ""));
    var title = NormalizeGalleryTitle(GetFormValue(form, "title", "未命名作品"));

    if (!jobs.TryGetValue(jobUuid, out var record) || !IsOwner(record, clientId))
    {
        return Results.NotFound();
    }

    if (record.Status != JobStatuses.Success || !File.Exists(record.OutputPath) || !File.Exists(record.PreviewPath))
    {
        return Results.BadRequest("job must be successful and still within the temporary download window.");
    }

    var galleryId = Guid.NewGuid().ToString("N");
    var outputExtension = Path.GetExtension(record.OutputPath);
    var galleryPreviewPath = Path.Combine(galleryPreviewDir, $"{galleryId}.png");
    var galleryOutputPath = Path.Combine(galleryOutputDir, $"{galleryId}{outputExtension}");
    File.Copy(record.PreviewPath, galleryPreviewPath, overwrite: false);
    File.Copy(record.OutputPath, galleryOutputPath, overwrite: false);

    var item = new GalleryRecord(
        galleryId,
        title,
        record.BoardType,
        record.OutputType,
        DateTimeOffset.UtcNow,
        0,
        galleryPreviewPath,
        galleryOutputPath
    );

    lock (galleryLock)
    {
        gallery.Add(item);
        SaveGallery(galleryIndexPath, gallery);
    }

    return Results.Ok(ToGalleryResponse(item));
});

app.MapGet("/api/gallery/{galleryId}/preview", (string galleryId) =>
{
    GalleryRecord? record;
    lock (galleryLock)
    {
        record = gallery.FirstOrDefault(item => item.GalleryId == galleryId);
    }

    return record is not null && File.Exists(record.PreviewPath)
        ? Results.File(record.PreviewPath, "image/png", $"{record.GalleryId}.png")
        : Results.NotFound();
});

app.MapGet("/api/gallery/{galleryId}/download", (string galleryId) =>
{
    GalleryRecord? record;
    lock (galleryLock)
    {
        record = gallery.FirstOrDefault(item => item.GalleryId == galleryId);
    }

    if (record is null || !File.Exists(record.OutputPath))
    {
        return Results.NotFound();
    }

    var contentType = record.OutputType == "uf2" ? "application/octet-stream" : "application/vnd.tomodachi.tdld";
    return Results.File(record.OutputPath, contentType, $"{record.GalleryId}.{record.OutputType}");
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

static string NormalizeGalleryBoardFilter(string? boardType)
{
    if (string.IsNullOrWhiteSpace(boardType) || boardType.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        return "all";
    }

    return NormalizeBoardType(boardType);
}

static string NormalizeClientId(string? clientId)
{
    var normalized = (clientId ?? "").Trim();
    return normalized.Length <= 80 && normalized.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
        ? normalized
        : "";
}

static string NormalizeGalleryTitle(string title)
{
    var normalized = title.Trim();
    if (string.IsNullOrWhiteSpace(normalized))
    {
        return "未命名作品";
    }

    return normalized.Length > 40 ? normalized[..40] : normalized;
}

static bool IsOwner(JobRecord record, string clientId) =>
    !string.IsNullOrWhiteSpace(clientId) && StringComparer.Ordinal.Equals(record.ClientId, clientId);

static SwitchVersion ParseSwitchVersion(string switchVersion)
{
    return switchVersion.ToLowerInvariant() switch
    {
        "switch1" => SwitchVersion.Switch1,
        _ => SwitchVersion.Switch2,
    };
}

static JobResponse ToOwnerJobResponse(JobRecord record) =>
    ToJobResponse(record, true, null);

static JobResponse ToJobResponse(JobRecord record, bool isOwner, int? anonymousIndex)
{
    var displayName = isOwner
        ? record.FileName
        : $"其他用户任务 #{anonymousIndex.GetValueOrDefault(record.QueuePosition)}";

    return new(
        isOwner ? record.JobUuid : $"anonymous-{anonymousIndex.GetValueOrDefault(record.QueuePosition)}",
        displayName,
        isOwner ? record.BoardType : "",
        isOwner ? record.OutputType : "",
        isOwner ? record.ColourMatcher : "",
        isOwner ? record.TspTimeLimit : 0,
        record.Status,
        record.QueuePosition,
        record.QueueAhead,
        record.ProgressPercent,
        record.CreatedAt,
        isOwner ? record.StartedAt : null,
        isOwner ? record.CompletedAt : null,
        isOwner && record.Status == JobStatuses.Success ? $"/api/jobs/{record.JobUuid}/download?clientId={record.ClientId}" : null,
        isOwner && File.Exists(record.PreviewPath) ? $"/api/jobs/{record.JobUuid}/preview?clientId={record.ClientId}" : null,
        isOwner ? record.Message : "其他用户任务正在处理中。"
    );
}

static GalleryResponse ToGalleryResponse(GalleryRecord record) =>
    new(
        record.GalleryId,
        record.Title,
        record.BoardType,
        record.OutputType,
        record.CreatedAt,
        record.Likes,
        $"/api/gallery/{record.GalleryId}/preview",
        $"/api/gallery/{record.GalleryId}/download"
    );

static List<GalleryRecord> LoadGallery(string galleryIndexPath)
{
    if (!File.Exists(galleryIndexPath))
    {
        return [];
    }

    try
    {
        return JsonSerializer.Deserialize<List<GalleryRecord>>(File.ReadAllText(galleryIndexPath)) ?? [];
    }
    catch
    {
        return [];
    }
}

static void SaveGallery(string galleryIndexPath, List<GalleryRecord> gallery)
{
    Directory.CreateDirectory(Path.GetDirectoryName(galleryIndexPath)!);
    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(galleryIndexPath, JsonSerializer.Serialize(gallery, options));
}

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
    string ClientId,
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
    public string ClientId { get; } = ClientId;
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

internal sealed record GalleryRecord(
    string GalleryId,
    string Title,
    string BoardType,
    string OutputType,
    DateTimeOffset CreatedAt,
    int Likes,
    string PreviewPath,
    string OutputPath
);
