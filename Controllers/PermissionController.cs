using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Backend_Gestion_Magasin_API.Services;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PermissionController : ControllerBase
    {
        private readonly IPermissionService _permissionService;

        public PermissionController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyPermissions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var permissions = await _permissionService.GetAllPermissionsAsync(userId);
            var peutPartagerLiens = await _permissionService.CanPartagerLiensAsync(userId);

            return Ok(permissions.Select(p => new
            {
                module = p.Module,
                canAccess = p.CanAccess,
                canWrite = p.CanWrite,
                // ADDITIF : la capacité « partager » est portée par le rôle, pas par un
                // module. Répétée sur chaque entrée pour ne PAS changer la forme du
                // tableau (le frontend fait data.find(p => p.module === ...) et
                // casserait sur un objet enveloppe). Le frontend la lit sur la première
                // entrée via le hook dédié.
                peutPartagerLiens
            }));
        }
    }
}
