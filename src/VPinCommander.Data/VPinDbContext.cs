using Microsoft.EntityFrameworkCore;
using VPinCommander.Core.Models;

namespace VPinCommander.Data;

public class VPinDbContext : DbContext
{
    public VPinDbContext(DbContextOptions<VPinDbContext> options) : base(options)
    {
    }

    public DbSet<GameTable> Tables => Set<GameTable>();
    public DbSet<Rom> Roms => Set<Rom>();
    public DbSet<MediaAsset> Media => Set<MediaAsset>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameTable>().HasIndex(t => t.FilePath).IsUnique();
        modelBuilder.Entity<Rom>().HasIndex(r => r.FilePath).IsUnique();
        modelBuilder.Entity<MediaAsset>().HasIndex(m => m.FilePath).IsUnique();
    }
}
