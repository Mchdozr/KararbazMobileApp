using Microsoft.AspNetCore.Mvc.Filters;
using Nereye.Api.Controllers;
using Nereye.Application.Auth;
using Nereye.Application.Common;

namespace Nereye.Api;

/// <summary>
/// Yorum, yanıt ve fotoğraf gibi başkalarına görünen içerik yalnızca e-postası doğrulanmış hesaptan yazılır.
/// Giriş ve okuma doğrulamasız da çalışır.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class VerifiedEmailAttribute : Attribute, IAsyncActionFilter
{
    public const string Message =
        "Yorum yazmak ve fotoğraf yüklemek için önce e-posta adresini doğrula. Doğrulama bağlantısını Profil › Hesap ve güvenlik'ten isteyebilirsin.";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
        if (!await auth.IsEmailVerifiedAsync(context.HttpContext.User.Id(), context.HttpContext.RequestAborted))
        {
            throw new ForbiddenException(Message);
        }

        await next();
    }
}
