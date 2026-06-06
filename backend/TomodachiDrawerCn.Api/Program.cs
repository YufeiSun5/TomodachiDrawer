using System.Collections.Concurrent;
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
Directory.CreateDirectory(uploadDir);
Directory.CreateDirectory(outputDir);

var jobs = new ConcurrentDictionary<string, JobRecord>();

app.MapGet("/api/health", () =>
    Results.Ok(new HealthResponse("ok", "tomodachi-cn-api", DateTimeOffset.UtcNow, "0.1.0"))
);

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
    var switchVersion = ParseSwitchVersion(GetFormValue(form, "switchVersion", "switch2"));
    var tspTimeLimit = int.TryParse(GetFormValue(form, "tspTimeLimit", "30"), out var parsedTspTimeLimit)
        ? parsedTspTimeLimit
        : 30;
    var colourMatcher = GetFormValue(form, "colourMatcher", "arbitrary");

    GeneratedDrawing generated;
    try
    {
        generated = await DrawingGenerator.GenerateAsync(
            new GenerateDrawingRequest(
                storedImagePath,
                outputPath,
                boardType,
                outputType,
                colourMatcher,
                tspTimeLimit,
                switchVersion
            )
        );
    }
    catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
    {
        return Results.BadRequest(ex.Message);
    }

    var record = new JobRecord(
        jobUuid,
        Path.GetFileName(image.FileName),
        boardType,
        generated.OutputType,
        colourMatcher,
        tspTimeLimit,
        JobStatuses.Success,
        0,
        DateTimeOffset.UtcNow,
        storedImagePath,
        outputPath,
        $"Generated with TomodachiDrawer.Core. TDLD={generated.TdldBytes} bytes, output={generated.OutputBytes} bytes, estimated draw time={generated.EstimatedDrawTime.TotalSeconds:F1}s."
    );

    jobs[jobUuid] = record;
    return Results.Ok(ToResponse(record));
});

app.MapGet("/api/jobs/{jobUuid}", (string jobUuid) =>
{
    return jobs.TryGetValue(jobUuid, out var record)
        ? Results.Ok(ToResponse(record))
        : Results.NotFound();
});

app.MapGet("/api/jobs/{jobUuid}/download", (string jobUuid) =>
{
    if (!jobs.TryGetValue(jobUuid, out var record) || !File.Exists(record.OutputPath))
    {
        return Results.NotFound();
    }

    var contentType = record.OutputType == "uf2" ? "application/octet-stream" : "application/vnd.tomodachi.tdld";
    return Results.File(record.OutputPath, contentType, $"{record.JobUuid}.{record.OutputType}");
});

app.Run();

static string GetFormValue(IFormCollection form, string key, string fallback)
{
    return form.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value.ToString()
        : fallback;
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
        record.CreatedAt,
        $"/api/jobs/{record.JobUuid}/download",
        record.Message
    );

internal sealed record JobRecord(
    string JobUuid,
    string FileName,
    string BoardType,
    string OutputType,
    string ColourMatcher,
    int TspTimeLimit,
    string Status,
    int QueuePosition,
    DateTimeOffset CreatedAt,
    string SourceImagePath,
    string OutputPath,
    string Message
);
