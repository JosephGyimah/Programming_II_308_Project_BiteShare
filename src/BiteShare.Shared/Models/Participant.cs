namespace BiteShare.Shared.Models;

public class Participant
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    // Null for authenticated users; set for anonymous "join without account" guests.
    public string? UserId { get; set; }
    public string? GuestToken { get; set; }

    public bool IsHost { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
