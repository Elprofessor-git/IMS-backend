using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using Backend_Gestion_Magasin_API.Services;

namespace Backend_Gestion_Magasin_API.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequireModulePermissionAttribute : Attribute, IAsyncActionFilter
    {
        // Un ou plusieurs modules séparés par des virgules (ex : "commandes,coupe").
        // L'accès est accordé si l'utilisateur a la permission sur AU MOINS UN des modules.
        private readonly string[] _modules;
        private readonly bool _requireWrite;

        public RequireModulePermissionAttribute(string modules, bool requireWrite = false)
        {
            _modules = modules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _requireWrite = requireWrite;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var permissionService = context.HttpContext.RequestServices
                .GetRequiredService<IPermissionService>();

            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            foreach (var module in _modules)
            {
                var (canAccess, canWrite) = await permissionService.GetPermissionAsync(userId, module);
                if (canAccess && (!_requireWrite || canWrite))
                {
                    await next();
                    return;
                }
            }

            var modulesLabel = string.Join(" ou ", _modules);
            context.Result = _requireWrite && _modules.Length == 1
                ? new ObjectResult(new { message = $"Droits d'écriture insuffisants sur le module '{modulesLabel}'." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                }
                : new ObjectResult(new { message = $"Accès refusé au module '{modulesLabel}'." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
        }
    }
}
