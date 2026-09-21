namespace BiteShare.Shared.DTOs;

public record AddCartItemRequest(Guid MenuItemId, int Quantity, string? Notes);
public record CartItemDto(Guid Id, Guid ParticipantId, string ParticipantName, Guid MenuItemId, string MenuItemName, decimal UnitPrice, int Quantity, string? Notes);

// Payload broadcast over OrderHub for cart-add/remove/update events
public record CartEvent(string EventType, Guid SessionId, CartItemDto Item);
