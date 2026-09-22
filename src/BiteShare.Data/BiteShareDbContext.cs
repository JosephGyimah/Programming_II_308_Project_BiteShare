using BiteShare.Shared.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BiteShare.Data;

public class BiteShareDbContext : IdentityDbContext<ApplicationUser>
{
    public BiteShareDbContext(DbContextOptions<BiteShareDbContext> options) : base(options) { }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasIndex(s => s.JoinCode).IsUnique();
            entity.Property(s => s.Name).HasMaxLength(200).IsRequired();
            entity.Property(s => s.JoinCode).HasMaxLength(12).IsRequired();
            entity.HasMany(s => s.Participants)
                  .WithOne()
                  .HasForeignKey(p => p.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.Orders)
                  .WithOne()
                  .HasForeignKey(o => o.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.Property(p => p.DisplayName).HasMaxLength(100).IsRequired();
            entity.HasMany(p => p.CartItems)
                  .WithOne()
                  .HasForeignKey(c => c.ParticipantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.Property(m => m.Name).HasMaxLength(200).IsRequired();
            entity.Property(m => m.Price).HasColumnType("decimal(10,2)");
            entity.HasIndex(m => m.SessionId);
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.Property(c => c.Quantity).HasDefaultValue(1);
            entity.HasOne<MenuItem>()
                  .WithMany()
                  .HasForeignKey(c => c.MenuItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(o => o.Subtotal).HasColumnType("decimal(10,2)");
            entity.Property(o => o.Tax).HasColumnType("decimal(10,2)");
            entity.Property(o => o.Tip).HasColumnType("decimal(10,2)");
            entity.Property(o => o.DeliveryFee).HasColumnType("decimal(10,2)");
            entity.HasMany(o => o.Receipts)
                  .WithOne()
                  .HasForeignKey(r => r.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.Property(r => r.AmountOwed).HasColumnType("decimal(10,2)");
            entity.HasIndex(r => new { r.OrderId, r.ParticipantId }).IsUnique();
        });
    }
}
