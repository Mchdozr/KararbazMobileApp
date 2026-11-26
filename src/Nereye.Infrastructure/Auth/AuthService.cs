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
using Nereye.Application.Email;
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
    private readonly IEmailSender _email;

    public AuthService(
        UserManager<AppUser> users,
        NereyeDbContext db,
        IOptions<JwtOptions> jwt,
        IClock clock,
        AvatarStorage avatars,
        IEmailSender email)
    {
        _email = email;
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

        if (!request.AcceptTerms)
        {
            throw new ValidationException("Devam etmek için kullanım koşullarını kabul et.");
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
        _db.ConsentLogs.Add(new ConsentLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = "terms",
            Granted = true,
            CreatedAt = _clock.UtcNow
        });
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
        }

        await _db.SaveChangesAsync(cancellationToken);

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

    /// <summary>
    /// Aynı token'la neredeyse aynı anda gelen ikinci yenileme (ör. iki sekme) hırsızlık sayılmaz: yalnızca reddedilir.
    /// Bu sürenin dışında iptal edilmiş bir token tekrar gelirse kullanıcının tüm oturumları kapanır.
    /// </summary>
    public static readonly TimeSpan ConcurrentRefreshGrace = TimeSpan.FromSeconds(10);

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh geçersiz.");
        }

        var now = _clock.UtcNow;
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null || stored.ExpiresAt <= now)
        {
            throw new UnauthorizedAccessException("Refresh geçersiz.");
        }

        if (stored.Revoked)
        {
            // Yerine token verilmemiş iptal (çıkış, şifre değişikliği, toplu iptal): token zaten ölü, yalnızca 401.
            // Bu durumda herkesi düşürmek, bir cihazdan çıkışın diğer cihazları da kapatmasına yol açardı.
            if (stored.ReplacedByTokenId is null)
            {
                throw new UnauthorizedAccessException("Refresh geçersiz.");
            }

            var concurrent = stored.RevokedAt > now - ConcurrentRefreshGrace;
            if (!concurrent)
            {
                // Yenilemeyle değiştirilmiş (kullanılmış) token tekrar geldi: çalınmış olabilir.
                // Bu kullanıcının bütün oturumları düşer.
                await RevokeAllAsync(stored.UserId, cancellationToken);
                throw new UnauthorizedAccessException("Güvenliğin için oturumların kapatıldı. Tekrar giriş yap.");
            }

            throw new UnauthorizedAccessException("Refresh geçersiz.");
        }

        var user = await _users.FindByIdAsync(stored.UserId.ToString())
                   ?? throw new UnauthorizedAccessException("Kullanıcı yok.");
        var nextId = Guid.NewGuid();
        stored.Revoked = true;
        stored.RevokedAt = now;
        stored.ReplacedByTokenId = user.IsBanned || user.DeletedAt is not null ? null : nextId;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Aynı token'ı başka bir istek az önce yeniledi; onun token'ı geçerli kalır.
            throw new UnauthorizedAccessException("Refresh geçersiz.");
        }

        if (user.IsBanned || user.DeletedAt is not null)
        {
            // Engellenen hesap elindeki refresh token ile oturumunu uzatamasın.
            throw new UnauthorizedAccessException("Hesap kullanılamıyor.");
        }

        return await IssueAsync(user, cancellationToken, nextId);
    }

    public async Task LogoutAsync(Guid? userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && (userId == null || t.UserId == userId),
            cancellationToken);
        if (stored is not null && !stored.Revoked)
        {
            stored.Revoked = true;
            stored.RevokedAt = _clock.UtcNow;
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

        // Yorumun puanı anonim kalır (mekan ortalaması bozulmasın); metni ve fotoğrafları silinir.
        var reviews = await _db.Reviews.Include(r => r.Photos).Where(r => r.UserId == userId).ToListAsync(cancellationToken);
        foreach (var review in reviews)
        {
            review.Text = null;
            foreach (var photo in review.Photos.ToList())
            {
                _avatars.DeleteUpload(photo.Url);
                _db.ReviewPhotos.Remove(photo);
            }
        }

        // Engelleme kayıtları iki yönde de kişiseldir.
        _db.UserBlocks.RemoveRange(await _db.UserBlocks
            .Where(b => b.BlockerUserId == userId || b.BlockedUserId == userId)
            .ToListAsync(cancellationToken));

        // Öne çıkan kart gösterim/tıklama kayıtları sayım için kalır, kullanıcıyla bağı kopar.
        foreach (var impression in await _db.PromotionImpressions.Where(i => i.UserId == userId).ToListAsync(cancellationToken))
        {
            impression.UserId = null;
        }

        // KVKK veri minimizasyonu: kişisel tercih ve konum kayıtları hesapla birlikte gider.
        _db.Favorites.RemoveRange(await _db.Favorites.Where(f => f.UserId == userId).ToListAsync(cancellationToken));
        _db.SavedPlaces.RemoveRange(await _db.SavedPlaces.Where(p => p.UserId == userId).ToListAsync(cancellationToken));
        // Listeler (paylaşılmış olanlar dahil) ve içerikleri: paylaşım bağlantısı da çalışmaz olur.
        _db.VenueLists.RemoveRange(await _db.VenueLists.Include(l => l.Items).Where(l => l.UserId == userId).ToListAsync(cancellationToken));
        // Bildirim cihazları ve gönderim kayıtları.
        var devices = await _db.PushDevices.Where(d => d.UserId == userId).ToListAsync(cancellationToken);
        var deviceIds = devices.Select(d => d.Id).ToList();
        _db.PushLogs.RemoveRange(await _db.PushLogs.Where(l => deviceIds.Contains(l.DeviceId)).ToListAsync(cancellationToken));
        _db.PushDevices.RemoveRange(devices);
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

    public const string DeletePurpose = "delete-account";

    public async Task RequestDeletionAsync(string email, Func<Guid, string, string> link, CancellationToken cancellationToken = default)
    {
        if (!_email.IsConfigured)
        {
            throw new ServiceUnavailableException("Hesap silme bağlantısı şu an gönderilemiyor.");
        }

        var normalized = AccountRules.NormalizeEmail(email);
        var user = normalized is null ? null : await _users.FindByEmailAsync(normalized);
        if (user is null || user.DeletedAt is not null)
        {
            return;
        }

        var token = await _users.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, DeletePurpose);
        var url = link(user.Id, token);
        var safe = System.Net.WebUtility.HtmlEncode(url);
        await _email.SendAsync(
            new EmailMessage(
                user.Email!,
                "Kararbaz hesabını silme onayı",
                $"<p>Merhaba,</p><p>Kararbaz hesabının silinmesi istendi. Onaylamak için aşağıdaki bağlantıyı aç ve sayfadaki düğmeye bas:</p>" +
                $"<p><a href=\"{safe}\">Hesabımı silmeyi onayla</a></p>" +
                "<p>Bağlantı 2 saat geçerlidir. Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin; hesabın silinmez.</p>",
                $"Kararbaz hesabının silinmesi istendi. Onaylamak için bağlantıyı aç ve sayfadaki düğmeye bas:\n{url}\n\n" +
                "Bağlantı 2 saat geçerlidir. Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin."),
            cancellationToken);
    }

    public async Task<bool> ConfirmDeletionAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || user.DeletedAt is not null || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!await _users.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, DeletePurpose, token))
        {
            return false;
        }

        await DeleteMeAsync(userId, cancellationToken);
        return true;
    }

    public async Task<bool> SendVerificationAsync(Guid userId, Func<Guid, string, string> link, CancellationToken cancellationToken = default)
    {
        if (!_email.IsConfigured)
        {
            throw new ServiceUnavailableException("E-posta doğrulama şu an kapalı.");
        }

        var user = await RequireUserAsync(userId);
        if (user.EmailConfirmed)
        {
            return false;
        }

        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        var url = link(user.Id, token);
        var safe = System.Net.WebUtility.HtmlEncode(url);
        await _email.SendAsync(
            new EmailMessage(
                user.Email!,
                "Kararbaz e-posta adresini doğrula",
                "<p>Merhaba,</p><p>Kararbaz'da yorum yazabilmek ve fotoğraf yükleyebilmek için e-posta adresini doğrula:</p>" +
                $"<p><a href=\"{safe}\">E-postamı doğrula</a></p>" +
                "<p>Bağlantı 2 saat geçerlidir. Bu hesabı sen açmadıysan bu e-postayı yok sayabilirsin.</p>",
                $"Kararbaz'da yorum yazabilmek ve fotoğraf yükleyebilmek için e-posta adresini doğrula:\n{url}\n\n" +
                "Bağlantı 2 saat geçerlidir. Bu hesabı sen açmadıysan bu e-postayı yok sayabilirsin."),
            cancellationToken);
        return true;
    }

    public async Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || user.DeletedAt is not null || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (user.EmailConfirmed)
        {
            return true;
        }

        return (await _users.ConfirmEmailAsync(user, token)).Succeeded;
    }

    public async Task<bool> IsEmailVerifiedAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.Users.AnyAsync(u => u.Id == userId && u.EmailConfirmed && u.DeletedAt == null, cancellationToken);

    public async Task RequestPasswordResetAsync(string email, Func<Guid, string, string> link, CancellationToken cancellationToken = default)
    {
        if (!_email.IsConfigured)
        {
            throw new ServiceUnavailableException(ResetClosed);
        }

        var normalized = AccountRules.NormalizeEmail(email);
        var user = normalized is null ? null : await _users.FindByEmailAsync(normalized);
        if (user is null || user.DeletedAt is not null || user.IsBanned)
        {
            return;
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var url = link(user.Id, token);
        var safe = System.Net.WebUtility.HtmlEncode(url);
        await _email.SendAsync(
            new EmailMessage(
                user.Email!,
                "Kararbaz şifreni sıfırla",
                "<p>Merhaba,</p><p>Kararbaz şifreni sıfırlamak için aşağıdaki bağlantıyı aç ve yeni şifreni yaz:</p>" +
                $"<p><a href=\"{safe}\">Yeni şifre belirle</a></p>" +
                "<p>Bağlantı 2 saat geçerlidir ve yalnızca bir kez kullanılabilir. Bu isteği sen yapmadıysan " +
                "bu e-postayı yok sayabilirsin; şifren değişmez.</p>",
                $"Kararbaz şifreni sıfırlamak için bağlantıyı aç ve yeni şifreni yaz:\n{url}\n\n" +
                "Bağlantı 2 saat geçerlidir ve yalnızca bir kez kullanılabilir. Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin."),
            cancellationToken);
    }

    public const string ResetClosed = "Şifre sıfırlama şu an kapalı.";

    public async Task<bool> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        if (AccountRules.PasswordProblem(newPassword) is { } problem)
        {
            throw new ValidationException(problem);
        }

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || user.DeletedAt is not null || user.IsBanned || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        // Identity token'ı güvenlik damgasına bağlıdır: başarılı sıfırlama damgayı yeniler, aynı bağlantı ikinci kez çalışmaz.
        var reset = await _users.ResetPasswordAsync(user, token, newPassword);
        if (!reset.Succeeded)
        {
            if (reset.Errors.Any(e => e.Code == "InvalidToken"))
            {
                return false;
            }

            throw new ValidationException(Describe(reset));
        }

        // Bağlantı gelen kutusuna ulaştı: adres de doğrulanmış sayılır. Kilit ve açık oturumlar sıfırlanır.
        user.EmailConfirmed = true;
        await _users.UpdateAsync(user);
        await _users.ResetAccessFailedCountAsync(user);
        await _users.SetLockoutEndDateAsync(user, null);
        await RevokeAllAsync(userId, cancellationToken);
        return true;
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
        for (var attempt = 0; ; attempt++)
        {
            var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && !t.Revoked).ToListAsync(cancellationToken);
            foreach (var token in tokens)
            {
                token.Revoked = true;
                token.RevokedAt = _clock.UtcNow;
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < 3)
            {
                // Bu sırada başka bir istek bir token'ı yeniledi: güncel hâli okuyup yeniden dene (yeni token da düşsün).
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }
        }
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
            await _db.SavedPlaces.CountAsync(p => p.UserId == user.Id, cancellationToken),
            user.EmailConfirmed);
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

    private async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken cancellationToken, Guid? tokenId = null)
    {
        var roles = await _users.GetRolesAsync(user);
        var access = CreateAccessToken(user, roles);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        // Yalnızca süresi dolanlar silinir. İptal edilmiş ama süresi dolmamış token'lar yeniden kullanım tespiti için kalır.
        var stale = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresAt <= _clock.UtcNow)
            .ToListAsync(cancellationToken);
        _db.RefreshTokens.RemoveRange(stale);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = tokenId ?? Guid.NewGuid(),
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
