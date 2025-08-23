using Microsoft.AspNetCore.Mvc;
using Backend_Gestion_Magasin_API.Services;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommandeController : ControllerBase
    {
        private readonly CommandeService _commandeService;

        public CommandeController(CommandeService commandeService)
        {
            _commandeService = commandeService;
        }

        [HttpGet]
        public async Task<ActionResult<List<CommandeClient>>> GetAll()
        {
            var commandes = await _commandeService.GetAllCommandesAsync();
            return Ok(commandes);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CommandeClient>> GetById(int id)
        {
            var commande = await _commandeService.GetCommandeByIdAsync(id);
            if (commande == null)
                return NotFound();
            return Ok(commande);
        }

        [HttpPost]
        public async Task<ActionResult<CommandeClient>> Create(CommandeClient commande)
        {
            await _commandeService.AddCommandeAsync(commande);
            return CreatedAtAction(nameof(GetById), new { id = commande.Id }, commande);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, CommandeClient commande)
        {
            if (id != commande.Id)
                return BadRequest();

            var result = await _commandeService.UpdateCommandeAsync(commande);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _commandeService.DeleteCommandeAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}

