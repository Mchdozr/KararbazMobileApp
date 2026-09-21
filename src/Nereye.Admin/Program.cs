using Microsoft.AspNetCore.Identity;
using Nereye.Admin.Components;
using Nereye.Application;
using Nereye.Infrastructure;
using Nereye.Infrastructure.Identity;
using Nereye.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNereyeInfrastructure(builder.Configuration);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/login";
    });
builder.Services.AddAuthorization();

var app = builder.Build();
await NereyeSeeder.SeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/account/login", async (HttpContext http, SignInManager<AppUser> signIn, UserManager<AppUser> users) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var user = await users.FindByEmailAsync(email);
    if (user is null || user.IsBanned || !await users.IsInRoleAsync(user, RoleNames.Admin))
    {
        return Results.Redirect("/login?error=1");
    }

    var result = await signIn.PasswordSignInAsync(user, password, true, false);
    return result.Succeeded ? Results.Redirect("/") : Results.Redirect("/login?error=1");
}).AllowAnonymous();

app.MapPost("/account/logout", async (SignInManager<AppUser> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.Redirect("/login");
}).RequireAuthorization();

app.Run();
