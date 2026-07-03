using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    [Route("api")]
    [ApiController]
    [Authorize]
    public class DocumentJointController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private static readonly HashSet<string> ContentTypesAutorises =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf",
                "image/jpeg",
                "image/png",
            };

        private const long TailleMaxOctets = 5L * 1024 * 1024; // 5 Mo

        public DocumentJointController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ── Achat ──────────────────────────────────────────────────────────

        // POST api/Achat/{achatId}/Documents
        [HttpPost("Achat/{achatId}/Documents")]
        public async Task<IActionResult> UploadAchat(int achatId, IFormFile file, [FromForm] TypeDocument type)
        {
            if (!await _context.Achats.AnyAsync(a => a.Id == achatId))
                return NotFound(new { message = $"Achat {achatId} introuvable." });

            return await Upload(file, type, achatId, null);
        }

        // GET api/Achat/{achatId}/Documents
        [HttpGet("Achat/{achatId}/Documents")]
        public async Task<IActionResult> ListAchat(int achatId)
        {
            if (!await _context.Achats.AnyAsync(a => a.Id == achatId))
                return NotFound();

            var docs = await _context.DocumentsJoints
                .Where(d => d.AchatId == achatId)
                .OrderByDescending(d => d.DateAjout)
                .Select(d => new
                {
                    d.Id,
                    d.Type,
                    d.NomFichier,
                    d.ContentType,
                    d.TailleOctets,
                    d.DateAjout,
                    d.AjoutePar,
                })
                .ToListAsync();

            return Ok(docs);
        }

        // GET api/Achat/{achatId}/Documents/{docId}/Download
        [HttpGet("Achat/{achatId}/Documents/{docId}/Download")]
        public async Task<IActionResult> DownloadAchat(int achatId, int docId)
        {
            var doc = await _context.DocumentsJoints
                .FirstOrDefaultAsync(d => d.Id == docId && d.AchatId == achatId);

            if (doc == null) return NotFound();

            return File(doc.Contenu, doc.ContentType, doc.NomFichier);
        }

        // DELETE api/Achat/{achatId}/Documents/{docId}
        [HttpDelete("Achat/{achatId}/Documents/{docId}")]
        public async Task<IActionResult> DeleteAchat(int achatId, int docId)
        {
            var doc = await _context.DocumentsJoints
                .FirstOrDefaultAsync(d => d.Id == docId && d.AchatId == achatId);

            if (doc == null) return NotFound();

            _context.DocumentsJoints.Remove(doc);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Importation ────────────────────────────────────────────────────

        // POST api/Importation/{importationId}/Documents
        [HttpPost("Importation/{importationId}/Documents")]
        public async Task<IActionResult> UploadImportation(int importationId, IFormFile file, [FromForm] TypeDocument type)
        {
            if (!await _context.Importations.AnyAsync(i => i.Id == importationId))
                return NotFound(new { message = $"Importation {importationId} introuvable." });

            return await Upload(file, type, null, importationId);
        }

        // GET api/Importation/{importationId}/Documents
        [HttpGet("Importation/{importationId}/Documents")]
        public async Task<IActionResult> ListImportation(int importationId)
        {
            if (!await _context.Importations.AnyAsync(i => i.Id == importationId))
                return NotFound();

            var docs = await _context.DocumentsJoints
                .Where(d => d.ImportationId == importationId)
                .OrderByDescending(d => d.DateAjout)
                .Select(d => new
                {
                    d.Id,
                    d.Type,
                    d.NomFichier,
                    d.ContentType,
                    d.TailleOctets,
                    d.DateAjout,
                    d.AjoutePar,
                })
                .ToListAsync();

            return Ok(docs);
        }

        // GET api/Importation/{importationId}/Documents/{docId}/Download
        [HttpGet("Importation/{importationId}/Documents/{docId}/Download")]
        public async Task<IActionResult> DownloadImportation(int importationId, int docId)
        {
            var doc = await _context.DocumentsJoints
                .FirstOrDefaultAsync(d => d.Id == docId && d.ImportationId == importationId);

            if (doc == null) return NotFound();

            return File(doc.Contenu, doc.ContentType, doc.NomFichier);
        }

        // DELETE api/Importation/{importationId}/Documents/{docId}
        [HttpDelete("Importation/{importationId}/Documents/{docId}")]
        public async Task<IActionResult> DeleteImportation(int importationId, int docId)
        {
            var doc = await _context.DocumentsJoints
                .FirstOrDefaultAsync(d => d.Id == docId && d.ImportationId == importationId);

            if (doc == null) return NotFound();

            _context.DocumentsJoints.Remove(doc);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Logique commune ────────────────────────────────────────────────

        private async Task<IActionResult> Upload(
            IFormFile file,
            TypeDocument type,
            int? achatId,
            int? importationId)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Aucun fichier fourni." });

            if (file.Length > TailleMaxOctets)
                return BadRequest(new
                {
                    message = $"Le fichier dépasse la taille maximale de 5 Mo " +
                              $"({file.Length / 1024.0 / 1024.0:F2} Mo reçus).",
                });

            if (!ContentTypesAutorises.Contains(file.ContentType))
                return BadRequest(new
                {
                    message = $"Type de fichier non autorisé : \"{file.ContentType}\". " +
                              "Types acceptés : application/pdf, image/jpeg, image/png.",
                });

            using var ms = new MemoryStream((int)file.Length);
            await file.CopyToAsync(ms);

            var doc = new DocumentJoint
            {
                AchatId        = achatId,
                ImportationId  = importationId,
                Type           = type,
                NomFichier     = file.FileName,
                ContentType    = file.ContentType,
                TailleOctets   = file.Length,
                Contenu        = ms.ToArray(),
                DateAjout      = DateTime.UtcNow,
                AjoutePar      = User.Identity?.Name,
            };

            _context.DocumentsJoints.Add(doc);
            await _context.SaveChangesAsync();

            // Retourne les métadonnées uniquement (pas le contenu binaire)
            return StatusCode(201, new
            {
                doc.Id,
                doc.Type,
                doc.NomFichier,
                doc.ContentType,
                doc.TailleOctets,
                doc.DateAjout,
                doc.AjoutePar,
            });
        }
    }
}
