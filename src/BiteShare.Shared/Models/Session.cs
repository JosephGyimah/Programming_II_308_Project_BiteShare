namespace BiteShare.Shared.Models;

public class Session
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HostUserId { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public SessionStatus Status { get; set; } = SessionStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OrderDeadlineUtc { get; set; }

    public ICollection<Participant> Participants { get; set; } = new List<Participant>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public enum SessionStatus
{
    Open,
    Submitted,
    Confirmed,
    Preparing,
    OutForDelivery,
    Delivered,
    Cancelled
}
