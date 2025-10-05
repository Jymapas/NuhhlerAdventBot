using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Note>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).IsRequired();
            e.Property(x => x.Username).HasMaxLength(64);
            e.HasIndex(x => new { x.UserId, x.UpdatedAt });
        });

        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.Lang).HasMaxLength(8);
        });
    }
}
