using Microsoft.AspNetCore.Mvc;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TacheController : ControllerBase
    {
        private readonly TacheService _tacheService;

        public TacheController(TacheService tacheService)
        {
            _tacheService = tacheService;
        }

        [HttpGet]
        public async Task<ActionResult<List<Tache>>> GetAll()
        {
            var taches = await _tacheService.GetAllTaches();
            return Ok(taches);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Tache>> GetById(int id)
        {
            var tache = await _tacheService.GetTacheById(id);
            if (tache == null)
                return NotFound();
            return Ok(tache);
        }

        [HttpPost]
        public async Task<ActionResult<Tache>> Create(Tache tache)
        {
            await _tacheService.AddTache(tache);
            return CreatedAtAction(nameof(GetById), new { id = tache.Id }, tache);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Tache tache)
        {
            if (id != tache.Id)
                return BadRequest();

            await _tacheService.UpdateTache(tache);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _tacheService.DeleteTache(id);
            return NoContent();
        }
    }
}
