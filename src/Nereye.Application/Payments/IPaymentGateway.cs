namespace Nereye.Application.Payments;

public sealed record PaymentChargeRequest(Guid CampaignId, decimal Amount, string Description);

public sealed record PaymentChargeResult(bool Succeeded, string ExternalId, string Provider);

public interface IPaymentGateway
{
    Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default);
}
