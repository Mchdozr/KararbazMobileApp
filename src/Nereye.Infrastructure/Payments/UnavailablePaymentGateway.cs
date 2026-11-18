using Nereye.Application.Common;
using Nereye.Application.Payments;

namespace Nereye.Infrastructure.Payments;

/// <summary>Yayında tahsilat yok (v1). Bir şekilde çağrılırsa para alınmış gibi davranmaz.</summary>
public sealed class UnavailablePaymentGateway : IPaymentGateway
{
    public Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default) =>
        throw new NotFoundException("Uygulama içi ödeme kapalı.");
}
