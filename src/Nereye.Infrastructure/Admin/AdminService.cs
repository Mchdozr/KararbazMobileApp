using Microsoft.EntityFrameworkCore;
using Nereye.Application.Admin;
using Nereye.Infrastructure.Identity;
using Nereye.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Nereye.Infrastructure.Places;

namespace Nereye.Infrastructure.Admin;

public sealed class AdminService : IAdminService
{
    private readonly NereyeDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly AvatarStorage _uploads;

    public AdminService(NereyeDbContext db, UserManager<AppUser> users, AvatarStorage uploads)
    {
        _uploads = uploads;
        _db = db;
        _users = users;
    }

    public async Task<IReadOnlyList<ReportAdminDto>> ListReportsAsync(bool unresolvedOnly, CancellationToken cancellationToken = default)
    {
        var q = _db.Reports.AsQueryable();
        if (unresolvedOnly)
        {
            q = q.Where(r => !r.IsResolved);
        }

        return await q.OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReportAdminDto(r.Id, r.ReporterUserId, r.VenueId, r.ReviewId, r.Reason, r.IsResolved, r.CreatedAt, r.PhotoUrl))
            .ToListAsync(cancellationToken);
    }

    public async Task ResolveReportAsync(Guid reportId, bool hideReview, CancellationToken cancellationToken = default)
    {
        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken)
                     ?? throw new InvalidOperationException("Şikayet yok.");
        report.IsResolved = true;
        var files = new List<string>();
        if (hideReview && report.PhotoUrl is { } photoUrl)
        {
            // Fotoğraf şikayeti: kare yorumdan ve mekan galerisinden çıkar, dosyası silinir.
            _db.ReviewPhotos.RemoveRange(await _db.ReviewPhotos.Where(p => p.Url == photoUrl).ToListAsync(cancellationToken));
            _db.VenuePhotos.RemoveRange(await _db.VenuePhotos.Where(p => p.Url == photoUrl).ToListAsync(cancellationToken));
            files.Add(photoUrl);
        }
        else if (hideReview && report.ReviewId is { } reviewId)
        {
            // Yorum şikayeti: yorum gizlenir; fotoğrafları gizli yorumla birlikte bağlantıdan da açılmasın diye silinir.
            var review = await _db.Reviews.Include(r => r.Photos).FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
            if (review is not null)
            {
                review.IsHidden = true;
                files.AddRange(review.Photos.Select(p => p.Url));
                _db.ReviewPhotos.RemoveRange(review.Photos);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        foreach (var url in files)
        {
            _uploads.DeleteUpload(url);
        }
    }

    public async Task BanUserAsync(Guid userId, bool banned, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString())
                   ?? throw new InvalidOperationException("Kullanıcı yok.");
        user.IsBanned = banned;
        await _users.UpdateAsync(user);
    }

    public IReadOnlyList<TagDictionaryItem> Tags() =>
    [
        new(VibeTag.Sessiz, "Sessiz"),
        new(VibeTag.Laptop, "Laptop uygun"),
        new(VibeTag.Manzarali, "Manzaralı"),
        new(VibeTag.Romantik, "Romantik"),
        new(VibeTag.Ucuz, "Uygun fiyat"),
        new(VibeTag.Premium, "Premium"),
        new(VibeTag.CocukDostu, "Çocuk dostu"),
        new(VibeTag.GecSaat, "Geç saat"),
        new(VibeTag.Tarihi, "Tarihi")
    ];

    public async Task<IReadOnlyList<UserAdminDto>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _users.Users
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.Email)
            .Select(u => new UserAdminDto(u.Id, u.Email ?? "", u.DisplayName, u.IsBanned))
            .ToListAsync(cancellationToken);
    }
}
