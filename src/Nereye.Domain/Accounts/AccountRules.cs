using System.Net.Mail;

namespace Nereye.Domain.Accounts;

public static class AccountRules
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxDisplayNameLength = 40;
    public const int MaxEmailLength = 254;

    public static string? NormalizeEmail(string? email)
    {
        var value = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || value.Length > MaxEmailLength || value.Contains(' '))
        {
            return null;
        }

        if (!MailAddress.TryCreate(value, out var parsed) || parsed.Address != value)
        {
            return null;
        }

        var at = value.LastIndexOf('@');
        var host = value[(at + 1)..];
        return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.') ? value : null;
    }

    /// <summary>Geçerliyse null, değilse kullanıcıya gösterilecek neden.</summary>
    public static string? PasswordProblem(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            return $"Şifre en az {MinPasswordLength} karakter olmalı.";
        }

        if (password.Length > MaxPasswordLength)
        {
            return "Şifre çok uzun.";
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return "Şifrede en az bir harf ve bir rakam olmalı.";
        }

        return null;
    }

    public static string? CleanDisplayName(string? name)
    {
        var value = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (value.Length == 0 || value.Any(char.IsControl))
        {
            return null;
        }

        return value.Length <= MaxDisplayNameLength ? value : value[..MaxDisplayNameLength].TrimEnd();
    }
}
