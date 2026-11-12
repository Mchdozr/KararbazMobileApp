namespace Nereye.Application.Common;

/// <summary>İstemciye güvenle gösterilebilen iş kuralı hatası. Mesaj Türkçe ve kullanıcıya dönüktür.</summary>
public class AppException : Exception
{
    public AppException(int statusCode, string message) : base(message) => StatusCode = statusCode;

    public int StatusCode { get; }
}

public sealed class ValidationException(string message) : AppException(400, message);

public sealed class NotFoundException(string message) : AppException(404, message);

public sealed class ForbiddenException(string message) : AppException(403, message);

public sealed class TooManyAttemptsException(string message) : AppException(429, message);
