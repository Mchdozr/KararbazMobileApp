namespace Nereye.Application.Push;

public sealed record PushMessage(string To, string Title, string Body, string? Url);

/// <summary>Unregistered: cihaz uygulamayı sildi ya da izni kaldırdı; kayıt silinmeli.</summary>
public sealed record PushResult(string To, bool Ok, bool Unregistered);

public interface IPushSender
{
    Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken = default);
}

public sealed record PushRegistration(string? Token, string? Platform);
