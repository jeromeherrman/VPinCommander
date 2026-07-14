namespace VPinCommander.Core.Services;

public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}

public interface IMediaManager
{
    /// <summary>
    /// Renames the media file to the table's name (keeping its extension and folder)
    /// so front-ends and the matcher pick it up, then updates the stored record.
    /// </summary>
    Task<OperationResult> AssignToTableAsync(int mediaAssetId, int tableId, CancellationToken ct = default);
}
