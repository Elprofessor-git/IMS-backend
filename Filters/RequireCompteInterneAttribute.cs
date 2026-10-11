using Backend_Gestion_Magasin_API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend_Gestion_Magasin_API.Filters
{
    /// <summary>
    /// Restreint une action aux COMPTES INTERNES (EstCompteInterne) : un compte
    /// externe n'accède à aucun endpoint du module Discussion, ni en lecture du
    /// module ni comme destinataire. Vérification en base (jamais depuis le jeton,
    /// comme la session) : le refus est immédiat dès que le compte bascule externe.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequireCompteInterneAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var db = context.HttpContext.RequestServices
                .GetRequiredService<ApplicationDbContext>();

            var interne = await db.Users.AnyAsync(
                u => u.Id == userId && u.EstCompteInterne,
                context.HttpContext.RequestAborted);

            if (!interne)
            {
                context.Result = new ObjectResult(new { message = "Ce module est réservé aux comptes internes." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }
    }
}