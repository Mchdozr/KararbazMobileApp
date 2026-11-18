using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nereye.Application;
using Nereye.Application.Promotions;

namespace Nereye.Api.Controllers;

[Authorize(Roles = RoleNames.VenueOwner)]
[ApiController]
[Route("api/promotions")]
public sealed class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotions;

    public PromotionsController(IPromotionService promotions) => _promotions = promotions;

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> Mine(CancellationToken ct) =>
        Ok(await _promotions.MineAsync(User.Id(), ct));

    [HttpPost]
    public async Task<ActionResult<PromotionDto>> Create(CreatePromotionRequest request, CancellationToken ct) =>
        Ok(await _promotions.CreateAsync(User.Id(), request, ct));

    /// <summary>
    /// Sandbox ödeme: v1'de uygulama içi tahsilat yok. Yayında (Production) bu rota yoktur;
    /// kampanyalar admin panelden açılır.
    /// </summary>
    [DevelopmentOnly]
    [HttpPost("{id:guid}/checkout")]
    public async Task<ActionResult<PromotionDto>> Checkout(Guid id, CancellationToken ct) =>
        Ok(await _promotions.CheckoutAsync(User.Id(), id, ct));
}
