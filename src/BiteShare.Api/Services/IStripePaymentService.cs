namespace BiteShare.Api.Services;

public record PaymentCaptureResult(bool Succeeded, string? PaymentIntentId, string? ErrorMessage);

public interface IStripePaymentService
{
    /// <summary>
    /// Creates and confirms a PaymentIntent for one participant's share of the order.
    /// In this MVP each participant pays with a test card / Stripe test-mode payment
    /// method collected client-side (Stripe.js) before this is called — the server
    /// only ever sees a payment_method id, never raw card data.
    /// </summary>
    Task<PaymentCaptureResult> CapturePaymentAsync(decimal amount, string currency, string paymentMethodId, string description, CancellationToken cancellationToken);
}
