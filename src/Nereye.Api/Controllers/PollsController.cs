using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Common;
using Nereye.Application.Polls;

namespace Nereye.Api.Controllers;

/// <summary>
/// Arkadaşlarla karar ver. Oluşturma ve oy hesap gerektirmez (bağlantıyı alan herkes oy verir); oy veren cihazın
/// rastgele anahtarı yalnızca özet olarak saklanır. Oylama 24 saat açık kalır.
/// </summary>
[ApiController]
[Route("api/polls")]
[AllowAnonymous]
public sealed class PollsController : ControllerBase
{
    private readonly IPollService _polls;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public PollsController(IPollService polls, IConfiguration config, IHostEnvironment env)
    {
        _polls = polls;
        _config = config;
        _env = env;
    }

    private string? WebBase => PublicPages.BaseUrl(HttpContext, _config, _env);

    [HttpPost]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<PollDto>> Create(CreatePollRequest request, CancellationToken ct) =>
        Ok(await _polls.CreateAsync(request, WebBase, ct));

    [HttpGet("{code}")]
    public async Task<ActionResult<PollDto>> Get(string code, [FromHeader(Name = "X-Voter")] string? voter, CancellationToken ct) =>
        await _polls.GetAsync(code, voter, WebBase, ct) is { } poll ? Ok(poll) : throw new NotFoundException("Oylama bulunamadı.");

    [HttpPost("{code}/votes")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<PollDto>> Vote(string code, VoteRequest request, CancellationToken ct) =>
        Ok(await _polls.VoteAsync(code, request, WebBase, ct));
}

/// <summary>Uygulaması olmayan arkadaş için web oylama sayfası: /k/{kod}.</summary>
[ApiController]
[Route("k")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PollPageController : ControllerBase
{
    private readonly IPollService _polls;

    public PollPageController(IPollService polls) => _polls = polls;

    [HttpGet("{code}")]
    public async Task<IResult> Page(string code, CancellationToken ct)
    {
        var poll = await _polls.GetAsync(code, null, null, ct);
        if (poll is null)
        {
            return PublicPages.Html("Oylama bulunamadı", "<p>Bağlantı hatalı ya da oylama silinmiş.</p>", 404);
        }

        var e = PublicPages.Encode;
        var body = new StringBuilder();
        body.Append(CultureInfo.InvariantCulture, $"""
            <p class="muted" id="status">{(poll.Expired ? "Oylama sona erdi." : "Bir mekana dokun, oyun hemen sayılır. İstersen sonra değiştirebilirsin.")}</p>
            <div id="options" data-code="{e(poll.Code)}" data-expired="{(poll.Expired ? "1" : "0")}">
            """);
        foreach (var o in poll.Options)
        {
            var img = o.PhotoUrl is null ? "" : $"""<img src="{e(o.PhotoUrl)}" alt="" loading="lazy">""";
            body.Append(CultureInfo.InvariantCulture, $"""
                <button class="opt" type="button" data-place="{e(o.PlaceId)}" aria-label="{e(o.Name)} için oy ver">
                  {img}
                  <span class="txt"><strong>{e(o.Name)}</strong><span class="muted">{e(o.Category)}</span>
                  <span class="bar"><i style="width:0%"></i></span></span>
                  <span class="count">{o.Votes}</span>
                </button>
                """);
        }

        body.Append(CultureInfo.InvariantCulture, $"""
            </div>
            <p class="muted" id="total">{poll.TotalVotes} oy</p>
            <p><a class="app" href="{e(poll.AppUrl)}">Nereye uygulamasında aç</a></p>
            """);
        body.Append(Script);

        const string head = """
            <style>
              .opt { display:flex; align-items:center; gap:12px; width:100%; text-align:left; margin:10px 0 0; padding:10px; min-height:72px;
                     background:#fffdf8; color:var(--ink); border:1.5px solid var(--line); border-radius:16px; font-weight:400; }
              .opt img { width:64px; height:64px; object-fit:cover; border-radius:12px; flex:none; }
              .opt .txt { flex:1; display:flex; flex-direction:column; gap:2px; }
              .opt .count { font-size:22px; font-weight:800; min-width:32px; text-align:right; }
              .opt .bar { display:block; height:6px; border-radius:3px; background:var(--line); margin-top:6px; overflow:hidden; }
              .opt .bar i { display:block; height:100%; background:var(--ink); transition:width .3s; }
              .opt.mine { border-color:var(--ink); box-shadow:0 0 0 2px var(--ink) inset; }
              .opt.win .count::after { content:" ★"; color:#c58a1a; }
              .opt:disabled { opacity:.7; cursor:default; }
              a.app { display:inline-block; margin-top:16px; color:var(--ink); font-weight:700; }
            </style>
            """;
        return PublicPages.Html(poll.Title, body.ToString(), head: head);
    }

    // Oy veren cihazın rastgele anahtarı yerel depoda; sayılar 5 sn'de bir tazelenir. Metinler textContent ile yazılır.
    private const string Script = """
            <script>
            (function () {
              var root = document.getElementById('options');
              var code = root.dataset.code;
              var key = localStorage.getItem('nereye.voter');
              if (!key) {
                var a = new Uint8Array(16); crypto.getRandomValues(a);
                key = Array.from(a, function (b) { return b.toString(16).padStart(2, '0'); }).join('');
                localStorage.setItem('nereye.voter', key);
              }
              function render(p) {
                var max = Math.max.apply(null, p.options.map(function (o) { return o.votes; }).concat([1]));
                p.options.forEach(function (o) {
                  var b = root.querySelector('[data-place="' + CSS.escape(o.placeId) + '"]');
                  if (!b) return;
                  b.querySelector('.count').textContent = o.votes;
                  b.querySelector('.bar i').style.width = Math.round(100 * o.votes / max) + '%';
                  b.classList.toggle('mine', p.myVote === o.placeId);
                  b.classList.toggle('win', p.winnerPlaceId === o.placeId);
                  b.disabled = p.expired;
                });
                document.getElementById('total').textContent = p.totalVotes + ' oy' + (p.expired ? ' · sona erdi' : '');
              }
              function load() {
                fetch('/api/polls/' + code, { headers: { 'X-Voter': key } }).then(function (r) { return r.ok ? r.json() : null; }).then(function (p) { if (p) render(p); });
              }
              root.addEventListener('click', function (ev) {
                var b = ev.target.closest('.opt');
                if (!b || b.disabled) return;
                fetch('/api/polls/' + code + '/votes', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ voterKey: key, placeId: b.dataset.place }) })
                  .then(function (r) { return r.json(); }).then(function (p) { if (p && p.options) render(p); else document.getElementById('status').textContent = (p && p.error) || 'Oy kaydedilemedi.'; });
              });
              load();
              setInterval(load, 5000);
            })();
            </script>
            """;
}
