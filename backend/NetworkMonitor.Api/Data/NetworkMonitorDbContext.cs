using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Data;

public sealed class NetworkMonitorDbContext(DbContextOptions<NetworkMonitorDbContext> options)
    : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var device = modelBuilder.Entity<Device>();

        device.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(100);

        device.Property(item => item.IpAddress)
            .IsRequired()
            .HasMaxLength(45);

        device.HasIndex(item => item.IpAddress)
            .IsUnique();

        device.Property(item => item.Description)
            .HasMaxLength(500);

        device.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(DeviceStatus.Unknown);

        device.Property(item => item.IsMonitoringEnabled)
            .HasDefaultValue(true);

        device.Property(item => item.CreatedAt)
            .IsRequired();

        device.Property(item => item.UpdatedAt)
            .IsRequired();
    }
}
