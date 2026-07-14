using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VPinCommander.Data;

/// <summary>Lets `dotnet ef migrations add` construct the context without the WPF app.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VPinDbContext>
{
    public VPinDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VPinDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new VPinDbContext(options);
    }
}
