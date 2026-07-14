using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VPinCommander.Core.Models;
using VPinCommander.Data.Export;
using Xunit;

namespace VPinCommander.Data.Tests;

public sealed class ExcelExporterTests : IDisposable
{
    private readonly string _folder;
    private readonly PooledDbContextFactory<VPinDbContext> _factory;

    public ExcelExporterTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "VPinCommanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);

        var options = new DbContextOptionsBuilder<VPinDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_folder, "test.db")}")
            .Options;
        _factory = new PooledDbContextFactory<VPinDbContext>(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();

        db.Tables.Add(new GameTable
        {
            Name = "Attack From Mars (Bally 1995)",
            FileName = "Attack From Mars (Bally 1995).vpx",
            FilePath = @"C:\Tables\Attack From Mars (Bally 1995).vpx",
            Format = TableFormat.VisualPinballX,
            RomName = "afm_113b",
            TableVersion = "2.0",
            HasBackglass = true,
        });
        db.Roms.Add(new Rom { Name = "afm_113b", FilePath = @"C:\Roms\afm_113b.zip" });
        db.Media.Add(new MediaAsset
        {
            FileName = "Attack From Mars (Bally 1995).png",
            FilePath = @"C:\Media\Wheel\Attack From Mars (Bally 1995).png",
            Category = MediaCategory.Wheel,
            MatchedTableName = "Attack From Mars (Bally 1995)",
        });
        db.SaveChanges();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task Export_produces_workbook_with_all_sheets_and_data()
    {
        var xlsxPath = Path.Combine(_folder, "export.xlsx");
        var exporter = new ExcelExporter(new InventoryStore(_factory));

        var result = await exporter.ExportAsync(xlsxPath);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(xlsxPath));

        using var workbook = new XLWorkbook(xlsxPath);
        var sheetNames = workbook.Worksheets.Select(w => w.Name).ToList();
        Assert.Equal(
            new[] { "Tables", "ROMs", "Media", "Front-end games", "Health", "Version history" },
            sheetNames);

        var tables = workbook.Worksheet("Tables");
        Assert.Equal("Name", tables.Cell(1, 1).GetString());
        Assert.Equal("Attack From Mars (Bally 1995)", tables.Cell(2, 1).GetString());
        Assert.Equal("afm_113b", tables.Cell(2, 3).GetString());
        Assert.Equal("Yes", tables.Cell(2, 6).GetString()); // B2S column

        Assert.Equal("afm_113b", workbook.Worksheet("ROMs").Cell(2, 1).GetString());
        Assert.Equal("Attack From Mars (Bally 1995).png", workbook.Worksheet("Media").Cell(2, 1).GetString());
    }
}
