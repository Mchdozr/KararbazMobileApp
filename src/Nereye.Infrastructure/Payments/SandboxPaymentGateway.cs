using Nereye.Application.Payments;

namespace Nereye.Infrastructure.Payments;

public sealed class SandboxPaymentGateway : IPaymentGateway
{
    public Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentChargeResult(true, $"sandbox-{request.CampaignId:N}", "sandbox"));
}
