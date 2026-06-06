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
    int QueueAhead,
    int ProgressPercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
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

public sealed record GalleryResponse(
    string GalleryId,
    string Title,
    string BoardType,
    string OutputType,
    DateTimeOffset CreatedAt,
    int Likes,
    string PreviewUrl,
    string DownloadUrl
);
