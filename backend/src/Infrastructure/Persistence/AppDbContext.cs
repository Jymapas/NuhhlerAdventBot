using System;
using Domain.Advent;
using Domain.Delivery;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AdventCampaign> AdventCampaigns => Set<AdventCampaign>();
    public DbSet<AdventDay> AdventDays => Set<AdventDay>();
    public DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dateOnlyConverter = new ValueConverter<DateOnly, DateTime>(
            d => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            d => DateOnly.FromDateTime(DateTime.SpecifyKind(d, DateTimeKind.Utc)));

        var timeOnlyConverter = new ValueConverter<TimeOnly, TimeSpan>(
            t => t.ToTimeSpan(),
            t => TimeOnly.FromTimeSpan(t));

        var nullableTimeOnlyConverter = new ValueConverter<TimeOnly?, TimeSpan?>(
            t => t.HasValue ? t.Value.ToTimeSpan() : null,
            t => t.HasValue ? TimeOnly.FromTimeSpan(t.Value) : null);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TelegramId).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(64);
            entity.Property(x => x.FirstName).HasMaxLength(128);
        });

        modelBuilder.Entity<AdventCampaign>(entity =>
        {
            entity.ToTable("AdventCampaigns");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.Property(x => x.StartDate).HasConversion(dateOnlyConverter);
            entity.Property(x => x.EndDate).HasConversion(dateOnlyConverter);
            entity.Property(x => x.DefaultSendTime).HasConversion(timeOnlyConverter);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.RecipientStatus).HasConversion<int>();
            entity.Property(x => x.BindToken)
                .HasMaxLength(64)
                .IsRequired(false);
            entity.HasIndex(x => new { x.OwnerUserId, x.StartDate, x.EndDate })
                .HasDatabaseName("IX_Campaign_Owner_Period");
            entity.HasIndex(x => x.BindToken).IsUnique(false);
            entity.HasMany(x => x.Days)
                .WithOne()
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        modelBuilder.Entity<AdventDay>(entity =>
        {
            entity.ToTable("AdventDays");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Date).HasConversion(dateOnlyConverter);
            entity.Property(x => x.OverrideSendTime)
                .HasConversion(nullableTimeOnlyConverter)
                .IsRequired(false);
            entity.Property(x => x.Text).HasMaxLength(4096);
            entity.HasIndex(x => new { x.CampaignId, x.Date }).IsUnique();
        });

        modelBuilder.Entity<DeliveryLog>(entity =>
        {
            entity.ToTable("DeliveryLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Date).HasConversion(dateOnlyConverter);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasIndex(x => new { x.CampaignId, x.Date, x.RecipientUserId, x.Status })
                .HasDatabaseName("IX_Delivery_Idem");
            entity.HasIndex(x => new { x.CampaignId, x.Date, x.RecipientUserId })
                .IsUnique()
                .HasFilter("Status = 0");
            entity.Property(x => x.CreatedAtUtc);
            entity.Property(x => x.LastAttemptAtUtc);
            entity.HasOne<AdventCampaign>()
                .WithMany()
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
