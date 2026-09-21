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
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            UserName = request.Email,
            DisplayName = request.DisplayName,
            CreatedAt = _clock.UtcNow
        };
        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
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
        var user = await _users.FindByEmailAsync(request.Email)
                   ?? throw new UnauthorizedAccessException("Geçersiz giriş.");
        if (user.IsBanned || user.DeletedAt is not null)
        {
            throw new UnauthorizedAccessException("Hesap kullanılamıyor.");
        }

        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException("Geçersiz giriş.");
        }

        return await IssueAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && !t.Revoked && t.ExpiresAt > _clock.UtcNow,
            cancellationToken) ?? throw new UnauthorizedAccessException("Refresh geçersiz.");

        stored.Revoked = true;
        var user = await _users.FindByIdAsync(stored.UserId.ToString())
                   ?? throw new UnauthorizedAccessException("Kullanıcı yok.");
        return await IssueAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
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

    public async Task DeleteMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString())
                   ?? throw new InvalidOperationException("Kullanıcı yok.");
        user.DeletedAt = _clock.UtcNow;
        user.Email = $"deleted-{userId:N}@nereye.invalid";
        user.UserName = user.Email;
        user.NormalizedEmail = user.Email.ToUpperInvariant();
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        user.AvatarUrl = null;
        _avatars.Delete(userId);
        await _users.UpdateAsync(user);

        var reviews = await _db.Reviews.Where(r => r.UserId == userId).ToListAsync(cancellationToken);
        foreach (var review in reviews)
        {
            review.Text = null;
        }

        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoked = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken cancellationToken)
    {
        var roles = await _users.GetRolesAsync(user);
        var access = CreateAccessToken(user, roles);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
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
