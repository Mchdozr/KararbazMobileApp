namespace Nereye.Application.Admin;

/// <param name="PhotoUrl">Fotoğraf şikayetiyse şikayet edilen kare (/media/uploads/...).</param>
public sealed record ReportAdminDto(Guid Id, Guid ReporterUserId, Guid? VenueId, Guid? ReviewId, string Reason, bool IsResolved, DateTime CreatedAt, string? PhotoUrl = null);

public sealed record UserAdminDto(Guid Id, string Email, string DisplayName, bool IsBanned);

public sealed record TagDictionaryItem(VibeTag Tag, string LabelTr);

public interface IAdminService
{
    Task<IReadOnlyList<UserAdminDto>> ListUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportAdminDto>> ListReportsAsync(bool unresolvedOnly, CancellationToken cancellationToken = default);
    Task ResolveReportAsync(Guid reportId, bool hideReview, CancellationToken cancellationToken = default);
    Task BanUserAsync(Guid userId, bool banned, CancellationToken cancellationToken = default);
    IReadOnlyList<TagDictionaryItem> Tags();
}
