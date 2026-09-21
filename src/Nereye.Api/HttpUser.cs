using System.Security.Claims;

namespace Nereye.Api.Controllers;

public static class HttpUser
{
    public static Guid Id(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException();
    }
}
