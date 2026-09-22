using Stripe;

namespace BiteShare.Api.Services;

public class StripePaymentService : IStripePaymentService
{
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(IConfiguration configuration, ILogger<StripePaymentService> logger)
    {
        _logger = logger;
        // StripeConfiguration.ApiKey is set once at startup (Program.cs) from
        // Stripe:SecretKey — never hard-code it here.
        _ = configuration;
    }

    public async Task<PaymentCaptureResult> CapturePaymentAsync(
        decimal amount, string currency, string paymentMethodId, string description, CancellationToken cancellationToken)
    {
        try
        {
            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero), // Stripe wants the smallest currency unit
                Currency = currency,
                PaymentMethod = paymentMethodId,
                Confirm = true,
                Description = description,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                }
            }, cancellationToken: cancellationToken);

            var succeeded = intent.Status is "succeeded" or "requires_capture";
            return new PaymentCaptureResult(succeeded, intent.Id, succeeded ? null : $"Payment status: {intent.Status}");
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe payment capture failed");
            return new PaymentCaptureResult(false, null, ex.Message);
        }
    }
}
