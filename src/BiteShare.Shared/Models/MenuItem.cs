namespace BiteShare.Shared.Models;

public class MenuItem
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool Available { get; set; } = true;
}
