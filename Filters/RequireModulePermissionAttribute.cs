using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using Backend_Gestion_Magasin_API.Services;

namespace Backend_Gestion_Magasin_API.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequireModulePermissionAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string _module;
        private readonly bool _requireWrite;

        public RequireModulePermissionAttribute(string module, bool requireWrite = false)
        {
            _module = module;
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

            var (canAccess, canWrite) = await permissionService.GetPermissionAsync(userId, _module);

            if (!canAccess)
            {
                context.Result = new ObjectResult(new { message = $"Accès refusé au module '{_module}'." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            if (_requireWrite && !canWrite)
            {
                context.Result = new ObjectResult(new { message = $"Droits d'écriture insuffisants sur le module '{_module}'." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }
    }
}
