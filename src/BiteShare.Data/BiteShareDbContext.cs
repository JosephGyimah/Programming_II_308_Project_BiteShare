using BiteShare.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace BiteShare.Data;

public class BiteShareDbContext : DbContext
{
    public BiteShareDbContext(DbContextOptions<BiteShareDbContext> options)
        : base(options)
    {
    }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Session>()
            .HasIndex(s => s.JoinCode)
            .IsUnique();

        modelBuilder.Entity<Session>()
            .HasMany(s => s.Participants)
            .WithOne()
            .HasForeignKey(p => p.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Session>()
            .HasMany(s => s.Orders)
            .WithOne()
            .HasForeignKey(o => o.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Participant>()
            .HasMany(p => p.CartItems)
            .WithOne()
            .HasForeignKey(c => c.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .HasMany(o => o.Receipts)
            .WithOne()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Decimal columns need explicit precision or EF/SQL Server will warn about
        // silent truncation of money values.
        modelBuilder.Entity<MenuItem>().Property(m => m.Price).HasPrecision(10, 2);
        modelBuilder.Entity<Order>().Property(o => o.Subtotal).HasPrecision(10, 2);
        modelBuilder.Entity<Order>().Property(o => o.Tax).HasPrecision(10, 2);
        modelBuilder.Entity<Order>().Property(o => o.Tip).HasPrecision(10, 2);
        modelBuilder.Entity<Order>().Property(o => o.DeliveryFee).HasPrecision(10, 2);
        modelBuilder.Entity<Receipt>().Property(r => r.AmountOwed).HasPrecision(10, 2);
    }
}
