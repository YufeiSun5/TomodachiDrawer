namespace TomodachiDrawerCn.Contracts;

public static class JobStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
}

public sealed record JobResponse(
    string JobUuid,
    string FileName,
    string BoardType,
    string OutputType,
    string ColourMatcher,
    int TspTimeLimit,
    string Status,
    int QueuePosition,
    DateTimeOffset CreatedAt,
    string? DownloadUrl,
    string? PreviewUrl,
    string? Message
);

public sealed record HealthResponse(
    string Status,
    string Service,
    DateTimeOffset CheckedAt,
    string Version
);
