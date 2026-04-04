using Microsoft.EntityFrameworkCore;
using Nexus.Hub.Domain.Entities;

namespace Nexus.Hub.Infrastructure.Data;

public class NexusDbContext(DbContextOptions<NexusDbContext> options) : DbContext(options)
{
    public DbSet<Spoke> Spokes => Set<Spoke>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<OutputStream> OutputStreams => Set<OutputStream>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureSpoke(modelBuilder);
        ConfigureProject(modelBuilder);
        ConfigureJob(modelBuilder);
        ConfigureMessage(modelBuilder);
        ConfigureOutputStream(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureAuditLog(modelBuilder);
    }

    private static void ConfigureSpoke(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Spoke>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).HasMaxLength(255).IsRequired();
            entity.Property(s => s.Status).HasMaxLength(50).HasConversion<string>().IsRequired();
            entity.Property(s => s.Capabilities).HasColumnType("jsonb").IsRequired();
            entity.Property(s => s.Config).HasColumnType("jsonb").IsRequired();
            entity.Property(s => s.Profile).HasColumnType("jsonb");

            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => s.LastSeen);

            entity.HasMany(s => s.Projects)
                .WithOne(p => p.Spoke)
                .HasForeignKey(p => p.SpokeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.Jobs)
                .WithOne(j => j.Spoke)
                .HasForeignKey(j => j.SpokeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.Messages)
                .WithOne(m => m.Spoke)
                .HasForeignKey(m => m.SpokeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureProject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.ExternalKey).HasMaxLength(255);
            entity.Property(p => p.Name).HasMaxLength(255).IsRequired();
            entity.Property(p => p.Status).HasMaxLength(50).HasConversion<string>().IsRequired();

            entity.HasIndex(p => p.SpokeId);
            entity.HasIndex(p => p.ExternalKey);
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => new { p.SpokeId, p.ExternalKey }).IsUnique();

            entity.HasMany(p => p.Jobs)
                .WithOne(j => j.Project)
                .HasForeignKey(j => j.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureJob(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Status).HasMaxLength(50).HasConversion<string>().IsRequired();
            entity.Property(j => j.Type).HasMaxLength(50).HasConversion<string>().IsRequired();
            entity.Property(j => j.ApprovedBy).HasMaxLength(255);
            entity.Property(j => j.ApprovalRequired).HasDefaultValue(false);

            entity.HasIndex(j => j.ProjectId);
            entity.HasIndex(j => j.SpokeId);
            entity.HasIndex(j => j.Status);
            entity.HasIndex(j => j.CreatedAt).IsDescending();

            entity.HasMany(j => j.OutputStreams)
                .WithOne(o => o.Job)
                .HasForeignKey(o => o.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(j => j.Messages)
                .WithOne(m => m.Job)
                .HasForeignKey(m => m.JobId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureMessage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Direction).HasMaxLength(50).HasConversion<string>().IsRequired();
            entity.Property(m => m.Content).IsRequired();

            entity.HasIndex(m => m.SpokeId);
            entity.HasIndex(m => m.JobId);
            entity.HasIndex(m => m.Timestamp).IsDescending();
        });
    }

    private static void ConfigureOutputStream(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutputStream>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Content).IsRequired();

            entity.HasIndex(o => new { o.JobId, o.Sequence }).IsUnique();
        });
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasMaxLength(255);
            entity.Property(u => u.Email).HasMaxLength(255).IsRequired();
            entity.Property(u => u.Name).HasMaxLength(255);

            entity.HasIndex(u => u.Email).IsUnique();
        });
    }

    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Action).HasMaxLength(255).IsRequired();
            entity.Property(a => a.UserId).HasMaxLength(255);
            entity.Property(a => a.Details).HasColumnType("jsonb");

            entity.HasIndex(a => a.Timestamp).IsDescending();

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Spoke)
                .WithMany()
                .HasForeignKey(a => a.SpokeId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
