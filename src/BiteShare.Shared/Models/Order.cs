namespace BiteShare.Shared.Models;

public class Order
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Tip { get; set; }
    public decimal DeliveryFee { get; set; }
    public SplitMode SplitMode { get; set; } = SplitMode.PerItem;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}

public enum OrderStatus
{
    Confirmed,
    Preparing,
    OutForDelivery,
    Delivered
}

// Decide explicitly per session — a common source of scope creep otherwise.
public enum SplitMode
{
    Equal,
    PerItem
}
