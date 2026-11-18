namespace Nereye.Application.Email;

public sealed record EmailMessage(string To, string Subject, string Html, string Text);

/// <summary>
/// İşlem e-postası (şifre sıfırlama, e-posta doğrulama, hesap silme onayı).
/// Sağlayıcı ayarlanmamışsa <see cref="IsConfigured"/> false döner ve bu akışlar kapalı kalır.
/// </summary>
public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
