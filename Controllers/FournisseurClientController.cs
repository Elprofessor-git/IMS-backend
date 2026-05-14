using Microsoft.AspNetCore.Mvc;
using Backend_Gestion_Magasin_API.Services;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Models;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FournisseurClientController : ControllerBase
    {
        private readonly FournisseurClientService _fournisseurClientService;

        public FournisseurClientController(FournisseurClientService fournisseurClientService)
        {
            _fournisseurClientService = fournisseurClientService;
        }

        [HttpGet]
        public async Task<ActionResult<List<FournisseurClient>>> GetAll()
        {
            var fournisseurClients = await _fournisseurClientService.GetAllAsync();
            return Ok(fournisseurClients);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<FournisseurClient>> GetById(int id)
        {
            var fournisseurClient = await _fournisseurClientService.GetByIdAsync(id);
            if (fournisseurClient == null)
                return NotFound();
            return Ok(fournisseurClient);
        }

        [HttpPost]
        public async Task<ActionResult<FournisseurClient>> Create(FournisseurClient fournisseurClient)
        {
            var result = await _fournisseurClientService.AddAsync(fournisseurClient);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, FournisseurClient fournisseurClient)
        {
            var result = await _fournisseurClientService.UpdateAsync(id, fournisseurClient);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _fournisseurClientService.DeleteAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
