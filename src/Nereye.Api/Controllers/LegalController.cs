using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nereye.Api.Controllers;

/// <summary>Gizlilik ve koşullar: uygulama JSON'u okur, mağaza ve web için aynı metin HTML sayfa olarak sunulur.</summary>
[ApiController]
[AllowAnonymous]
public sealed class LegalController : ControllerBase
{
    private readonly IConfiguration _config;

    public LegalController(IConfiguration config) => _config = config;

    [HttpGet("api/legal")]
    public ActionResult<object> Json()
    {
        var contact = LegalTexts.Contact(_config);
        Response.Headers.CacheControl = "public, max-age=3600";
        return Ok(new
        {
            privacy = LegalTexts.Privacy(contact),
            terms = LegalTexts.Terms(contact),
            contact,
            draft = LegalTexts.DraftNote
        });
    }

    [HttpGet("gizlilik")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IResult Privacy() => Page(LegalTexts.Privacy(LegalTexts.Contact(_config)));

    [HttpGet("kosullar")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IResult Terms() => Page(LegalTexts.Terms(LegalTexts.Contact(_config)));

    private static IResult Page(LegalTexts.Document doc)
    {
        var body = new StringBuilder();
        body.Append($"<p>{PublicPages.Encode(doc.Lead)}</p>");
        foreach (var s in doc.Sections)
        {
            body.Append($"<div class=\"card\"><h2>{PublicPages.Encode(s.Title)}</h2><p>{PublicPages.Encode(s.Body)}</p></div>");
        }

        body.Append($"<p class=\"muted\">{PublicPages.Encode(LegalTexts.DraftNote)}</p>");
        return PublicPages.Html(doc.Title, body.ToString());
    }
}
