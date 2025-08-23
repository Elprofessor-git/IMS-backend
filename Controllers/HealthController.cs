using Microsoft.AspNetCore.Mvc;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                service = "Backend_Gestion_Magasin_API"
            });
        }
    }
}