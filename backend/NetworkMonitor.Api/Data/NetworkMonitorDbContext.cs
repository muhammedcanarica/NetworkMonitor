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
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailNotificationSettings> EmailNotificationSettings => Set<EmailNotificationSettings>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<NetworkCredential> NetworkCredentials => Set<NetworkCredential>();
    public DbSet<SnmpMonitoringProfile> SnmpMonitoringProfiles => Set<SnmpMonitoringProfile>();
    public DbSet<SnmpMonitoredInterface> SnmpMonitoredInterfaces => Set<SnmpMonitoredInterface>();
    public DbSet<InterfaceTrafficSample> InterfaceTrafficSamples => Set<InterfaceTrafficSample>();
    public DbSet<InterfaceBandwidthThreshold> InterfaceBandwidthThresholds => Set<InterfaceBandwidthThreshold>();

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
        incident.HasIndex(item => new { item.DeviceId, item.Type })
            .HasDatabaseName("IX_Incidents_OpenDeviceType")
            .HasFilter("\"Status\" = 'Open' AND \"SnmpMonitoredInterfaceId\" IS NULL")
            .IsUnique();
        incident.HasIndex(item => new { item.DeviceId, item.SnmpMonitoredInterfaceId, item.Type })
            .HasDatabaseName("IX_Incidents_OpenInterfaceType")
            .HasFilter("\"Status\" = 'Open' AND \"SnmpMonitoredInterfaceId\" IS NOT NULL")
            .IsUnique();
        incident.HasOne(item => item.Device).WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.Cascade);
        incident.HasOne(item => item.SnmpMonitoredInterface).WithMany().HasForeignKey(item => item.SnmpMonitoredInterfaceId).OnDelete(DeleteBehavior.SetNull);

        var notification = modelBuilder.Entity<Notification>();
        notification.Property(item => item.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        notification.Property(item => item.Title).IsRequired().HasMaxLength(100);
        notification.Property(item => item.Message).IsRequired().HasMaxLength(500);
        notification.Property(item => item.CreatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        notification.Property(item => item.ReadAt).HasConversion(value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : (long?)null, value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);
        notification.HasIndex(item => item.CreatedAt);
        notification.HasIndex(item => item.ReadAt);
        notification.HasIndex(item => new { item.IncidentId, item.Type }).IsUnique();
        notification.HasOne(item => item.Incident).WithMany().HasForeignKey(item => item.IncidentId).OnDelete(DeleteBehavior.SetNull);
        notification.HasOne(item => item.Device).WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.SetNull);

        var emailSettings = modelBuilder.Entity<EmailNotificationSettings>();
        emailSettings.Property(item => item.Host).IsRequired().HasMaxLength(255);
        emailSettings.Property(item => item.TlsMode).HasConversion<string>().HasMaxLength(24).IsRequired();
        emailSettings.Property(item => item.Username).HasMaxLength(255);
        emailSettings.Property(item => item.FromAddress).IsRequired().HasMaxLength(320);
        emailSettings.Property(item => item.FromName).HasMaxLength(100);
        emailSettings.Property(item => item.RecipientAddresses).IsRequired().HasMaxLength(4000);
        emailSettings.Property(item => item.CreatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        emailSettings.Property(item => item.UpdatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();

        var deliveryAttempt = modelBuilder.Entity<NotificationDeliveryAttempt>();
        deliveryAttempt.Property(item => item.Channel).HasConversion<string>().HasMaxLength(16).IsRequired();
        deliveryAttempt.Property(item => item.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        deliveryAttempt.Property(item => item.LastErrorCode).HasConversion<string>().HasMaxLength(32);
        deliveryAttempt.Property(item => item.LastAttemptAt).HasConversion(value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : (long?)null, value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);
        deliveryAttempt.Property(item => item.SentAt).HasConversion(value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : (long?)null, value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);
        deliveryAttempt.Property(item => item.NextAttemptAt).HasConversion(value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : (long?)null, value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);
        deliveryAttempt.Property(item => item.CreatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        deliveryAttempt.Property(item => item.UpdatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        deliveryAttempt.HasIndex(item => new { item.NotificationId, item.Channel }).IsUnique();
        deliveryAttempt.HasIndex(item => new { item.Status, item.NextAttemptAt });
        deliveryAttempt.HasOne(item => item.Notification).WithMany().HasForeignKey(item => item.NotificationId).OnDelete(DeleteBehavior.Cascade);

        var credential = modelBuilder.Entity<NetworkCredential>();
        credential.Property(item => item.Name).IsRequired().HasMaxLength(100);
        credential.Property(item => item.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        credential.Property(item => item.Username).HasMaxLength(100);
        credential.Property(item => item.ProtectedSecret).IsRequired();
        credential.Property(item => item.CreatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        credential.Property(item => item.UpdatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        credential.HasIndex(item => item.Name).IsUnique();
        credential.HasOne(item => item.Device).WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.SetNull);

        var snmpProfile = modelBuilder.Entity<SnmpMonitoringProfile>();
        snmpProfile.Property(item => item.CreatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        snmpProfile.Property(item => item.UpdatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        snmpProfile.HasIndex(item => item.DeviceId).IsUnique();
        snmpProfile.HasOne(item => item.Device).WithMany().HasForeignKey(item => item.DeviceId).OnDelete(DeleteBehavior.Cascade);
        snmpProfile.HasOne(item => item.Credential).WithMany().HasForeignKey(item => item.CredentialId).OnDelete(DeleteBehavior.Restrict);

        var monitoredInterface = modelBuilder.Entity<SnmpMonitoredInterface>();
        monitoredInterface.Property(item => item.InterfaceName).IsRequired().HasMaxLength(255);
        monitoredInterface.Property(item => item.Description).HasMaxLength(500);
        monitoredInterface.Property(item => item.CreatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        monitoredInterface.Property(item => item.LastOperationalState).HasConversion<string>().HasMaxLength(16);
        monitoredInterface.HasIndex(item => new { item.SnmpMonitoringProfileId, item.InterfaceIndex }).IsUnique();
        monitoredInterface.HasOne(item => item.Profile).WithMany(item => item.Interfaces).HasForeignKey(item => item.SnmpMonitoringProfileId).OnDelete(DeleteBehavior.Cascade);

        var trafficSample = modelBuilder.Entity<InterfaceTrafficSample>();
        trafficSample.Property(item => item.Timestamp).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        trafficSample.Property(item => item.OperStatus).IsRequired().HasMaxLength(16);
        trafficSample.Property(item => item.AdminStatus).HasMaxLength(16);
        trafficSample.HasIndex(item => new { item.SnmpMonitoredInterfaceId, item.Timestamp });
        trafficSample.HasOne(item => item.MonitoredInterface).WithMany(item => item.Samples).HasForeignKey(item => item.SnmpMonitoredInterfaceId).OnDelete(DeleteBehavior.Cascade);

        var bandwidthThreshold = modelBuilder.Entity<InterfaceBandwidthThreshold>();
        bandwidthThreshold.ToTable(table => table.HasCheckConstraint(
            "CK_InterfaceBandwidthThresholds_AtLeastOneThreshold",
            "\"InboundThresholdBitsPerSecond\" IS NOT NULL OR \"OutboundThresholdBitsPerSecond\" IS NOT NULL"));
        bandwidthThreshold.Property(item => item.CreatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        bandwidthThreshold.Property(item => item.UpdatedAt).HasConversion(value => value.ToUnixTimeMilliseconds(), value => DateTimeOffset.FromUnixTimeMilliseconds(value)).IsRequired();
        bandwidthThreshold.HasIndex(item => item.SnmpMonitoredInterfaceId).IsUnique();
        bandwidthThreshold.HasOne(item => item.MonitoredInterface).WithOne(item => item.BandwidthThreshold).HasForeignKey<InterfaceBandwidthThreshold>(item => item.SnmpMonitoredInterfaceId).OnDelete(DeleteBehavior.Cascade);
    }
}
