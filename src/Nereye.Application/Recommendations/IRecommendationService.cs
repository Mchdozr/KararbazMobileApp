namespace Nereye.Application.Recommendations;

public interface IRecommendationService
{
    Task<RecommendationResponse> RecommendAsync(RecommendationQuery query, CancellationToken cancellationToken = default);
}
