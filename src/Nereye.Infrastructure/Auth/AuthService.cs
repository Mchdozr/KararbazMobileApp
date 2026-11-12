using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nereye.Application;
using Nereye.Application.Auth;
using Nereye.Application.Common;
using Nereye.Domain.Accounts;
using Nereye.Infrastructure.Identity;
using Nereye.Infrastructure.Persistence;
using Nereye.Infrastructure.Places;

namespace Nereye.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "nereye";
    public string Audience { get; set; } = "nereye-app";
    public string SigningKey { get; set; } = "dev-only-change-me-32-chars-minimum!!";
    public int AccessMinutes { get; set; } = 30;
    public int RefreshDays { get; set; } = 14;
}

public sealed class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _users;
    private readonly NereyeDbContext _db;
    private readonly JwtOptions _jwt;
    private readonly IClock _clock;
    private readonly AvatarStorage _avatars;

    public AuthService(UserManager<AppUser> users, NereyeDbContext db, IOptions<JwtOptions> jwt, IClock clock, AvatarStorage avatars)
    {
        _users = users;
        _db = db;
        _jwt = jwt.Value;
        _clock = clock;
        _avatars = avatars;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = AccountRules.NormalizeEmail(request.Email)
                    ?? throw new ValidationException("Geçerli bir e-posta yaz.");
        if (AccountRules.PasswordProblem(request.Password) is { } problem)
        {
            throw new ValidationException(problem);
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            DisplayName = AccountRules.CleanDisplayName(request.DisplayName) ?? "Gezgin",
            CreatedAt = _clock.UtcNow
        };
        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new ValidationException(Describe(created));
        }

        await _users.AddToRoleAsync(user, RoleNames.User);
        if (request.AcceptPrivacy)
        {
            _db.ConsentLogs.Add(new ConsentLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Purpose = "kvkk",
                Granted = true,
                CreatedAt = _clock.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await IssueAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Kayıt yoksa ve şifre yanlışsa aynı mesaj: hangi e-postaların kayıtlı olduğu sızmasın.
        var email = AccountRules.NormalizeEmail(request.Email)
                    ?? throw new UnauthorizedAccessException(BadLogin);
        var user = await _users.FindByEmailAsync(email)
                   ?? throw new UnauthorizedAccessException(BadLogin);
        if (user.IsBanned || user.DeletedAt is not null)
        {
            throw new UnauthorizedAccessException("Hesap kullanılamıyor.");
        }

        if (await _users.IsLockedOutAsync(user))
        {
            throw new TooManyAttemptsException("Çok fazla hatalı deneme. Birkaç dakika sonra tekrar dene.");
        }

        if (string.IsNullOrEmpty(request.Password) || !await _users.CheckPasswordAsync(user, request.Password))
        {
            await _users.AccessFailedAsync(user);
            throw new UnauthorizedAccessException(BadLogin);
        }

        await _users.ResetAccessFailedCountAsync(user);
        return await IssueAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh geçersiz.");
        }

        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && !t.Revoked && t.ExpiresAt > _clock.UtcNow,
            cancellationToken) ?? throw new UnauthorizedAccessException("Refresh geçersiz.");

        stored.Revoked = true;
        var user = await _users.FindByIdAsync(stored.UserId.ToString())
                   ?? throw new UnauthorizedAccessException("Kullanıcı yok.");
        if (user.IsBanned || user.DeletedAt is not null)
        {
            // Engellenen hesap elindeki refresh token ile oturumunu uzatamasın.
            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Hesap kullanılamıyor.");
        }

        return await IssueAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(
            t => t.UserId == userId && t.TokenHash == hash,
            cancellationToken);
        if (stored is not null)
        {
            stored.Revoked = true;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(userId);
        return await ToProfileAsync(user, cancellationToken);
    }

    public async Task<ProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var name = AccountRules.CleanDisplayName(request.DisplayName)
                   ?? throw new ValidationException("Bir ad yaz.");
        var user = await RequireUserAsync(userId);
        user.DisplayName = name;
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new ValidationException(Describe(updated));
        }

        return await ToProfileAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (AccountRules.PasswordProblem(request.NewPassword) is { } problem)
        {
            throw new ValidationException(problem);
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            throw new ValidationException("Yeni şifre eskisiyle aynı olamaz.");
        }

        var user = await RequireUserAsync(userId);
        var changed = await _users.ChangePasswordAsync(user, request.CurrentPassword ?? "", request.NewPassword);
        if (!changed.Succeeded)
        {
            throw new ValidationException(Describe(changed));
        }

        await RevokeAllAsync(userId, cancellationToken);
        return await IssueAsync(user, cancellationToken);
    }

    public async Task DeleteMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(userId);
        user.DeletedAt = _clock.UtcNow;
        user.Email = $"deleted-{userId:N}@nereye.invalid";
        user.UserName = user.Email;
        user.NormalizedEmail = user.Email.ToUpperInvariant();
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        user.DisplayName = "Silinmiş hesap";
        user.AvatarUrl = null;
        user.PasswordHash = null;
        _avatars.Delete(userId);
        await _users.UpdateAsync(user);

        var reviews = await _db.Reviews.Where(r => r.UserId == userId).ToListAsync(cancellationToken);
        foreach (var review in reviews)
        {
            review.Text = null;
        }

        // KVKK veri minimizasyonu: kişisel tercih ve konum kayıtları hesapla birlikte gider.
        _db.Favorites.RemoveRange(await _db.Favorites.Where(f => f.UserId == userId).ToListAsync(cancellationToken));
        _db.SavedPlaces.RemoveRange(await _db.SavedPlaces.Where(p => p.UserId == userId).ToListAsync(cancellationToken));
        var pref = await _db.UserPreferences
            .Include(p => p.Intents)
            .Include(p => p.Vibes)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (pref is not null)
        {
            _db.UserPreferences.Remove(pref);
        }

        await RevokeAllAsync(userId, cancellationToken);
    }

    private const string BadLogin = "E-posta veya şifre hatalı.";

    private async Task<AppUser> RequireUserAsync(Guid userId)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || user.DeletedAt is not null)
        {
            throw new UnauthorizedAccessException("Kullanıcı yok.");
        }

        return user;
    }

    private async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && !t.Revoked).ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoked = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProfileDto> ToProfileAsync(AppUser user, CancellationToken cancellationToken)
    {
        var roles = await _users.GetRolesAsync(user);
        return new ProfileDto(
            user.Id,
            user.Email ?? "",
            user.DisplayName,
            user.AvatarUrl,
            roles.ToList(),
            user.CreatedAt,
            await _db.Reviews.CountAsync(r => r.UserId == user.Id && !r.IsHidden, cancellationToken),
            await _db.Favorites.CountAsync(f => f.UserId == user.Id, cancellationToken),
            await _db.SavedPlaces.CountAsync(p => p.UserId == user.Id, cancellationToken));
    }

    private static string Describe(IdentityResult result)
    {
        var codes = result.Errors.Select(e => e.Code).ToList();
        if (codes.Any(c => c is "DuplicateEmail" or "DuplicateUserName"))
        {
            return "Bu e-posta zaten kayıtlı. Giriş yapmayı dene.";
        }

        if (codes.Contains("PasswordMismatch"))
        {
            return "Mevcut şifre hatalı.";
        }

        if (codes.Any(c => c.StartsWith("Password", StringComparison.Ordinal)))
        {
            return "Şifre en az 8 karakter olmalı, harf ve rakam içermeli.";
        }

        if (codes.Any(c => c is "InvalidEmail" or "InvalidUserName"))
        {
            return "Geçerli bir e-posta yaz.";
        }

        return "İşlem tamamlanamadı. Bilgileri kontrol et.";
    }

    private async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken cancellationToken)
    {
        var roles = await _users.GetRolesAsync(user);
        var access = CreateAccessToken(user, roles);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var stale = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && (t.Revoked || t.ExpiresAt <= _clock.UtcNow))
            .ToListAsync(cancellationToken);
        _db.RefreshTokens.RemoveRange(stale);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(refresh),
            CreatedAt = _clock.UtcNow,
            ExpiresAt = _clock.UtcNow.AddDays(_jwt.RefreshDays)
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new AuthResponse(access, refresh, user.Id, user.DisplayName, roles.ToList(), user.AvatarUrl);
    }

    private string CreateAccessToken(AppUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(ClaimTypes.Name, user.DisplayName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var token = new JwtSecurityToken(
            _jwt.Issuer,
            _jwt.Audience,
            claims,
            expires: _clock.UtcNow.AddMinutes(_jwt.AccessMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
