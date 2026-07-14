using ClosedXML.Excel;
using VPinCommander.Core.Health;
using VPinCommander.Core.Persistence;
using VPinCommander.Core.Services;

namespace VPinCommander.Data.Export;

public sealed class ExcelExporter : IExcelExporter
{
    private readonly IInventoryStore _store;

    public ExcelExporter(IInventoryStore store)
    {
        _store = store;
    }

    public async Task<OperationResult> ExportAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            var tables = await _store.GetTablesAsync(ct);
            var roms = await _store.GetRomsAsync(ct);
            var media = await _store.GetMediaAsync(ct);
            var games = await _store.GetFrontEndGamesAsync(ct: ct);
            var history = await _store.GetVersionHistoryAsync(limit: 10_000, ct: ct);
            var findings = HealthReportBuilder.Build(tables, roms, media, games);

            using var workbook = new XLWorkbook();

            var tablesSheet = workbook.AddWorksheet("Tables");
            WriteHeader(tablesSheet, "Name", "Format", "ROM", "Version", "Author",
                "B2S", "PuP-Pack", "DOF", "AltColor", "AltSound", "Missing", "Size (bytes)", "Modified (UTC)", "Path");
            int row = 2;
            foreach (var t in tables)
            {
                tablesSheet.Cell(row, 1).SetValue(t.Name);
                tablesSheet.Cell(row, 2).SetValue(t.Format.ToString());
                tablesSheet.Cell(row, 3).SetValue(t.RomName ?? string.Empty);
                tablesSheet.Cell(row, 4).SetValue(t.TableVersion ?? string.Empty);
                tablesSheet.Cell(row, 5).SetValue(t.Author ?? string.Empty);
                tablesSheet.Cell(row, 6).SetValue(YesNo(t.HasBackglass));
                tablesSheet.Cell(row, 7).SetValue(YesNo(t.HasPupPack));
                tablesSheet.Cell(row, 8).SetValue(YesNo(t.HasDofConfig));
                tablesSheet.Cell(row, 9).SetValue(YesNo(t.HasAltColor));
                tablesSheet.Cell(row, 10).SetValue(YesNo(t.HasAltSound));
                tablesSheet.Cell(row, 11).SetValue(YesNo(t.IsMissing));
                tablesSheet.Cell(row, 12).SetValue(t.FileSizeBytes);
                tablesSheet.Cell(row, 13).SetValue(t.FileModifiedUtc);
                tablesSheet.Cell(row, 14).SetValue(t.FilePath);
                row++;
            }

            var romsSheet = workbook.AddWorksheet("ROMs");
            WriteHeader(romsSheet, "ROM", "Missing", "Size (bytes)", "Modified (UTC)", "Path");
            row = 2;
            foreach (var r in roms)
            {
                romsSheet.Cell(row, 1).SetValue(r.Name);
                romsSheet.Cell(row, 2).SetValue(YesNo(r.IsMissing));
                romsSheet.Cell(row, 3).SetValue(r.FileSizeBytes);
                romsSheet.Cell(row, 4).SetValue(r.FileModifiedUtc);
                romsSheet.Cell(row, 5).SetValue(r.FilePath);
                row++;
            }

            var mediaSheet = workbook.AddWorksheet("Media");
            WriteHeader(mediaSheet, "File", "Category", "Matched table", "Missing", "Size (bytes)", "Path");
            row = 2;
            foreach (var m in media)
            {
                mediaSheet.Cell(row, 1).SetValue(m.FileName);
                mediaSheet.Cell(row, 2).SetValue(m.Category.ToString());
                mediaSheet.Cell(row, 3).SetValue(m.MatchedTableName ?? string.Empty);
                mediaSheet.Cell(row, 4).SetValue(YesNo(m.IsMissing));
                mediaSheet.Cell(row, 5).SetValue(m.FileSizeBytes);
                mediaSheet.Cell(row, 6).SetValue(m.FilePath);
                row++;
            }

            var gamesSheet = workbook.AddWorksheet("Front-end games");
            WriteHeader(gamesSheet, "Game", "Source", "System", "File", "ROM", "Year", "Manufacturer", "Match", "Visible");
            row = 2;
            foreach (var g in games)
            {
                gamesSheet.Cell(row, 1).SetValue(g.DisplayName);
                gamesSheet.Cell(row, 2).SetValue(g.Source.ToString());
                gamesSheet.Cell(row, 3).SetValue(g.EmulatorName);
                gamesSheet.Cell(row, 4).SetValue(g.GameFileName);
                gamesSheet.Cell(row, 5).SetValue(g.RomName ?? string.Empty);
                gamesSheet.Cell(row, 6).SetValue(g.Year ?? string.Empty);
                gamesSheet.Cell(row, 7).SetValue(g.Manufacturer ?? string.Empty);
                gamesSheet.Cell(row, 8).SetValue(g.MatchStatus.ToString());
                gamesSheet.Cell(row, 9).SetValue(YesNo(g.Visible));
                row++;
            }

            var healthSheet = workbook.AddWorksheet("Health");
            WriteHeader(healthSheet, "Severity", "Category", "Item", "Detail");
            row = 2;
            foreach (var f in findings)
            {
                healthSheet.Cell(row, 1).SetValue(f.Severity.ToString());
                healthSheet.Cell(row, 2).SetValue(f.Category);
                healthSheet.Cell(row, 3).SetValue(f.Item);
                healthSheet.Cell(row, 4).SetValue(f.Detail);
                row++;
            }

            var historySheet = workbook.AddWorksheet("Version history");
            WriteHeader(historySheet, "Recorded (UTC)", "Table", "Change", "Old version", "New version", "File modified (UTC)", "Path");
            row = 2;
            foreach (var c in history)
            {
                historySheet.Cell(row, 1).SetValue(c.RecordedUtc);
                historySheet.Cell(row, 2).SetValue(c.TableName);
                historySheet.Cell(row, 3).SetValue(c.Kind.ToString());
                historySheet.Cell(row, 4).SetValue(c.OldVersion ?? string.Empty);
                historySheet.Cell(row, 5).SetValue(c.NewVersion ?? string.Empty);
                historySheet.Cell(row, 6).SetValue(c.FileModifiedUtc);
                historySheet.Cell(row, 7).SetValue(c.FilePath);
                row++;
            }

            foreach (var sheet in workbook.Worksheets)
                sheet.Columns().AdjustToContents(1, Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 1, 200));

            workbook.SaveAs(filePath);
            return OperationResult.Ok(
                $"Exported {tables.Count} tables, {roms.Count} ROMs, {media.Count} media files, "
                + $"{games.Count} front-end games, {findings.Count} health findings to {filePath}");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Export failed: {ex.Message}");
        }
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).SetValue(headers[i]);
        sheet.Row(1).Style.Font.SetBold();
        sheet.SheetView.FreezeRows(1);
    }

    private static string YesNo(bool value) => value ? "Yes" : string.Empty;
}
