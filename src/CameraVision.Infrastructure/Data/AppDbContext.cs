using System.Text.Json;
using CameraVision.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CameraVision.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<CaptureRule> CaptureRules => Set<CaptureRule>();
    public DbSet<Capture> Captures => Set<Capture>();
    public DbSet<AlertSettings> AlertSettings => Set<AlertSettings>();
    public DbSet<CaptureAlertSettings> CaptureAlertSettings => Set<CaptureAlertSettings>();
    public DbSet<CaptureAlertLog> CaptureAlertLogs => Set<CaptureAlertLog>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<HealthAlertSettings> HealthAlertSettings => Set<HealthAlertSettings>();
    public DbSet<AdminAlertSettings> AdminAlertSettings => Set<AdminAlertSettings>();
    public DbSet<CameraHealthEvent> CameraHealthEvents => Set<CameraHealthEvent>();
    public DbSet<SystemAlertEvent> SystemAlertEvents => Set<SystemAlertEvent>();
    public DbSet<WorkerStatus> WorkerStatus => Set<WorkerStatus>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        modelBuilder.Entity<Tenant>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<Camera>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
            e.HasIndex(c => c.TenantId);
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(c => c.StreamUrl).HasMaxLength(500).IsRequired();
            e.Property(c => c.SubStreamUrl).HasMaxLength(500);
            e.Property(c => c.PreferredStream).HasMaxLength(10).IsRequired();
            e.Property(c => c.IpAddress).HasMaxLength(64);
            e.Property(c => c.ProcessorStatus).HasMaxLength(20);
        });

        modelBuilder.Entity<CaptureRule>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(100).IsRequired();
            e.Property(r => r.Classes)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
            e.HasIndex(r => r.TenantId);
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(r => r.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Capture>(e =>
        {
            e.Property(c => c.CameraName).HasMaxLength(100).IsRequired();
            e.Property(c => c.ObjectClass).HasMaxLength(100).IsRequired();
            e.Property(c => c.FilePath).HasMaxLength(500).IsRequired();
            e.Property(c => c.ThumbnailPath).HasMaxLength(500);
            e.Property(c => c.AlertChannels).HasMaxLength(50);
            e.HasIndex(c => c.FilePath).IsUnique();
            e.HasIndex(c => c.StartedAt);
            e.HasIndex(c => c.CameraName);
            e.HasIndex(c => c.ObjectClass);
            e.HasIndex(c => c.TenantId);
            e.HasOne<Camera>()
                .WithMany()
                .HasForeignKey(c => c.CameraId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne<CaptureRule>()
                .WithMany()
                .HasForeignKey(c => c.AlertRuleId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AlertSettings>(e =>
        {
            e.Property(s => s.Channel).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(s => new { s.TenantId, s.Channel }).IsUnique();
            e.Property(s => s.Recipients)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(s => s.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemSettings>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.SmtpSecurity).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<HealthAlertSettings>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<AdminAlertSettings>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Emails)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
            e.Property(s => s.WhatsAppNumbers)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
        });

        modelBuilder.Entity<SystemAlertEvent>(e =>
        {
            e.Property(s => s.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(s => s.Detail).HasMaxLength(300);
            e.HasIndex(s => s.OccurredAt);
        });

        modelBuilder.Entity<WorkerStatus>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Device).HasMaxLength(200);
        });

        modelBuilder.Entity<CaptureAlertSettings>(e =>
        {
            e.HasIndex(s => s.TenantId).IsUnique();
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(s => s.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CaptureAlertLog>(e =>
        {
            e.Property(l => l.ErrorMessage).HasMaxLength(500);
            e.HasIndex(l => l.CaptureId);
            e.HasOne<Capture>()
                .WithMany()
                .HasForeignKey(l => l.CaptureId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CaptureRule>()
                .WithMany()
                .HasForeignKey(l => l.CaptureRuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CameraHealthEvent>(e =>
        {
            e.Property(h => h.CameraName).HasMaxLength(100).IsRequired();
            e.Property(h => h.Condition).HasConversion<string>().HasMaxLength(20);
            e.Property(h => h.Detail).HasMaxLength(200);
            e.HasIndex(h => new { h.CameraId, h.OccurredAt });
            e.HasIndex(h => h.OccurredAt);
            e.HasIndex(h => h.TenantId);
            e.HasOne<Camera>()
                .WithMany()
                .HasForeignKey(h => h.CameraId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(h => h.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.Property(u => u.Username).HasMaxLength(64).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(u => u.TenantId);
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
