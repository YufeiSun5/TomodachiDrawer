using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
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

    var outputType = GetFormValue(form, "outputType", "tdld").ToLowerInvariant() == "uf2" ? "uf2" : "tdld";
    var outputPath = Path.Combine(outputDir, $"{jobUuid}.{outputType}");
    await File.WriteAllBytesAsync(outputPath, BuildPlaceholderOutput(jobUuid, image.FileName, outputType));

    var record = new JobRecord(
        jobUuid,
        Path.GetFileName(image.FileName),
        GetFormValue(form, "boardType", "rp2040"),
        outputType,
        GetFormValue(form, "colourMatcher", "arbitrary"),
        int.TryParse(GetFormValue(form, "tspTimeLimit", "30"), out var tspTimeLimit) ? tspTimeLimit : 30,
        JobStatuses.Success,
        0,
        DateTimeOffset.UtcNow,
        storedImagePath,
        outputPath,
        "MVP placeholder output generated. Core drawing integration is the next implementation step."
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

static byte[] BuildPlaceholderOutput(string jobUuid, string fileName, string outputType)
{
    var header = outputType == "tdld" ? "TDLD" : "UF2_PLACEHOLDER";
    var body = $"{header}\njob={jobUuid}\nsource={fileName}\ngenerated_at={DateTimeOffset.UtcNow:O}\n";
    return Encoding.UTF8.GetBytes(body);
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
