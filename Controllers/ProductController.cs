using Microsoft.AspNetCore.Mvc;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Product API is working!");
        }
    }
}

