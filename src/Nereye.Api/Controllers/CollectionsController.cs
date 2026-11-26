using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Collections;
using Nereye.Application.Common;

namespace Nereye.Api.Controllers;

/// <summary>Adlandırılmış listeler ("Kahve rotam"). Liste sahibine özeldir; paylaşılırsa bağlantıyla okunur.</summary>
[ApiController]
[Authorize]
[Route("api/me/collections")]
public sealed class CollectionsController : ControllerBase
{
    private readonly ICollectionService _lists;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public CollectionsController(ICollectionService lists, IConfiguration config, IHostEnvironment env)
    {
        _lists = lists;
        _config = config;
        _env = env;
    }

    private string? WebBase => PublicPages.BaseUrl(HttpContext, _config, _env);

    /// <summary>placeId verilirse her listede o mekan olup olmadığı da döner (mekan sayfasındaki "Listeye ekle").</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CollectionSummaryDto>>> List([FromQuery] string? placeId, CancellationToken ct) =>
        Ok(await _lists.ListAsync(User.Id(), placeId, ct));

    [HttpPost]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<CollectionDto>> Create(NameRequest body, CancellationToken ct) =>
        Ok(await _lists.CreateAsync(User.Id(), body.Name, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CollectionDto>> Get(Guid id, CancellationToken ct) => Ok(await _lists.GetAsync(User.Id(), id, WebBase, ct));

    [HttpPut("{id:guid}")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<CollectionDto>> Rename(Guid id, NameRequest body, CancellationToken ct) =>
        Ok(await _lists.RenameAsync(User.Id(), id, body.Name, WebBase, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _lists.DeleteAsync(User.Id(), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/items")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<CollectionDto>> Add(Guid id, AddItemRequest body, CancellationToken ct) =>
        Ok(await _lists.AddItemAsync(User.Id(), id, body, WebBase, ct));

    [HttpDelete("{id:guid}/items/{placeId}")]
    public async Task<IActionResult> Remove(Guid id, string placeId, CancellationToken ct)
    {
        await _lists.RemoveItemAsync(User.Id(), id, placeId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult<ShareDto>> Share(Guid id, CancellationToken ct) => Ok(await _lists.ShareAsync(User.Id(), id, WebBase, ct));

    [HttpDelete("{id:guid}/share")]
    public async Task<IActionResult> Unshare(Guid id, CancellationToken ct)
    {
        await _lists.UnshareAsync(User.Id(), id, ct);
        return NoContent();
    }
}

/// <summary>Paylaşılan liste: uygulama için JSON, uygulaması olmayan için /l/{kod} web sayfası.</summary>
[ApiController]
[AllowAnonymous]
public sealed class PublicListsController : ControllerBase
{
    private readonly ICollectionService _lists;

    public PublicListsController(ICollectionService lists) => _lists = lists;

    [HttpGet("api/lists/{code}")]
    [EnableRateLimiting("osm")]
    public async Task<ActionResult<PublicListDto>> Get(string code, CancellationToken ct) =>
        await _lists.PublicAsync(code, ct) is { } list ? Ok(list) : throw new NotFoundException("Liste bulunamadı ya da artık paylaşılmıyor.");

    [HttpGet("l/{code}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IResult> Page(string code, CancellationToken ct)
    {
        var list = await _lists.PublicAsync(code, ct);
        if (list is null)
        {
            return PublicPages.Html("Liste bulunamadı", "<p>Bağlantı hatalı ya da liste artık paylaşılmıyor.</p>", 404);
        }

        var e = PublicPages.Encode;
        var body = new StringBuilder();
        body.Append(CultureInfo.InvariantCulture, $"""<p class="muted">{list.Items.Count} mekan · Kararbaz ile hazırlandı</p>""");
        foreach (var i in list.Items)
        {
            var img = i.PhotoUrl is null ? "" : $"""<img src="{e(i.PhotoUrl)}" alt="" loading="lazy">""";
            body.Append(CultureInfo.InvariantCulture, $"""
                <a class="item" href="nereyego://venue/{Uri.EscapeDataString(i.PlaceId)}">
                  {img}<span><strong>{e(i.Name)}</strong><span class="muted">{e(i.Category)}</span></span>
                </a>
                """);
        }

        body.Append(CultureInfo.InvariantCulture, $"""<p><a class="app" href="nereyego://list/{e(code)}">Kararbaz uygulamasında aç</a></p>""");
        const string head = """
            <style>
              .item { display:flex; align-items:center; gap:12px; margin-top:10px; padding:10px; background:#fffdf8;
                      border:1px solid var(--line); border-radius:16px; color:var(--ink); text-decoration:none; }
              .item img { width:64px; height:64px; object-fit:cover; border-radius:12px; flex:none; }
              .item span { display:flex; flex-direction:column; }
              a.app { display:inline-block; margin-top:16px; color:var(--ink); font-weight:700; }
            </style>
            """;
        return PublicPages.Html(list.Name, body.ToString(), head: head);
    }
}
