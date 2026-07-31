using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Data;

public sealed class NetworkMonitorDbContext(DbContextOptions<NetworkMonitorDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Device> Devices => Set<Device>();

    public DbSet<CheckResult> CheckResults => Set<CheckResult>();

    public DbSet<ConfigBackup> ConfigBackups => Set<ConfigBackup>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<NetworkCredential> NetworkCredentials => Set<NetworkCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
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

        var checkResult = modelBuilder.Entity<CheckResult>();

        checkResult.Property(item => item.CheckedAt)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        checkResult.Property(item => item.DeviceStatus)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        checkResult.Property(item => item.FailureReason)
            .HasMaxLength(100);

        checkResult.HasIndex(item => new { item.DeviceId, item.CheckedAt });

        checkResult.HasOne(item => item.Device)
            .WithMany(deviceItem => deviceItem.CheckResults)
            .HasForeignKey(item => item.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        var configBackup = modelBuilder.Entity<ConfigBackup>();

        configBackup.Property(item => item.IpAddress)
            .IsRequired()
            .HasMaxLength(45);

        configBackup.Property(item => item.Vendor)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        configBackup.Property(item => item.Configuration)
            .IsRequired();

        configBackup.Property(item => item.Hash)
            .IsRequired()
            .HasMaxLength(64);

        configBackup.Property(item => item.CapturedAt)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        configBackup.Property(item => item.CreatedAt)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        configBackup.HasIndex(item => new { item.IpAddress, item.CreatedAt });
        configBackup.HasIndex(item => new { item.DeviceId, item.CreatedAt });

        configBackup.HasOne<Device>()
            .WithMany()
            .HasForeignKey(item => item.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        var incident = modelBuilder.Entity<Incident>();
        incident.Property(item => item.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        incident.Property(item => item.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        incident.Property(item => item.Summary).IsRequired().HasMaxLength(200);
        incident.Property(item => item.StartedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        incident.Property(item => item.ResolvedAt).HasConversion(value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : (long?)null, value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);
        incident.Property(item => item.CreatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        incident.Property(item => item.UpdatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        incident.HasIndex(item => new { item.DeviceId, item.Status, item.StartedAt });
        incident.HasIndex(item => new { item.DeviceId, item.Type }).HasFilter("\"Status\" = 'Open'").IsUnique();
        incident.HasOne(item => item.Device).WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.Cascade);

        var credential = modelBuilder.Entity<NetworkCredential>();
        credential.Property(item => item.Name).IsRequired().HasMaxLength(100);
        credential.Property(item => item.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        credential.Property(item => item.Username).HasMaxLength(100);
        credential.Property(item => item.ProtectedSecret).IsRequired();
        credential.Property(item => item.CreatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        credential.Property(item => item.UpdatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        credential.HasIndex(item => item.Name).IsUnique();
        credential.HasOne(item => item.Device).WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.SetNull);
    }
}
