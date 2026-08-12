namespace MiniTransfertPortable;

internal sealed record TransferSnapshot(
    long BytesSent,
    int ActiveDownloads,
    int CompletedDownloads,
    DateTimeOffset? LastActivity);
