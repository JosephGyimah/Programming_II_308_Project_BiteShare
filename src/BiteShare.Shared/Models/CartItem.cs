namespace BiteShare.Shared.Models;

public class CartItem
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid MenuItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
