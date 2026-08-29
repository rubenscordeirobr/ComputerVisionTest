using System.Text.Json;
using CameraVision.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CameraVision.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<CaptureSettings> CaptureSettings => Set<CaptureSettings>();
    public DbSet<Capture> Captures => Set<Capture>();
    public DbSet<AlertSettings> AlertSettings => Set<AlertSettings>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
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

        modelBuilder.Entity<Camera>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.StreamUrl).HasMaxLength(500).IsRequired();
            e.Property(c => c.IpAddress).HasMaxLength(64);
        });

        modelBuilder.Entity<CaptureSettings>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.TrackedClasses)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
        });

        modelBuilder.Entity<Capture>(e =>
        {
            e.Property(c => c.CameraName).HasMaxLength(100).IsRequired();
            e.Property(c => c.ObjectClass).HasMaxLength(100).IsRequired();
            e.Property(c => c.FilePath).HasMaxLength(500).IsRequired();
            e.Property(c => c.ThumbnailPath).HasMaxLength(500);
            e.HasIndex(c => c.FilePath).IsUnique();
            e.HasIndex(c => c.StartedAt);
            e.HasIndex(c => c.CameraName);
            e.HasIndex(c => c.ObjectClass);
            e.HasOne<Camera>()
                .WithMany()
                .HasForeignKey(c => c.CameraId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AlertSettings>(e =>
        {
            e.Property(s => s.Channel).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(s => s.Channel).IsUnique();
            e.Property(s => s.Recipients)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
            e.Property(s => s.TriggerClasses)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
        });

        modelBuilder.Entity<SystemSettings>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.SmtpSecurity).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.Property(u => u.Username).HasMaxLength(64).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.Property(u => u.PasswordHash).IsRequired();
        });
    }
}
