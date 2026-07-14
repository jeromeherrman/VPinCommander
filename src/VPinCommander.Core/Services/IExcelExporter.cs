namespace VPinCommander.Core.Services;

public interface IExcelExporter
{
    /// <summary>Writes the whole inventory (tables, ROMs, media, front-end games, health, history) to an .xlsx file.</summary>
    Task<OperationResult> ExportAsync(string filePath, CancellationToken ct = default);
}
