using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend_Gestion_Magasin_API.Filters;
using Backend_Gestion_Magasin_API.Models;
using Backend_Gestion_Magasin_API.Data;
using Backend_Gestion_Magasin_API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend_Gestion_Magasin_API.Controllers
{
    /// <summary>
    /// Parc machines à coudre — module indépendant (inventaire + maintenance).
    /// CRUD référentiel + journal d'interventions, sur le modèle ChaineProductionController
    /// (DTOs dédiés, projections .Select(), permission module « machines »).
    /// Aucune méthode protégée n'est touchée.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MachineController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MachineController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [RequireModulePermission("machines", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<MachineDto>>> GetMachines()
        {
            var machines = await _context.Machines
                .OrderBy(m => m.CodeMachine)
                .Select(m => new MachineDto
                {
                    Id = m.Id,
                    CodeMachine = m.CodeMachine,
                    Marque = m.Marque,
                    Modele = m.Modele,
                    NumeroSerie = m.NumeroSerie,
                    TypeMachine = m.TypeMachine.ToString(),
                    DateAcquisition = m.DateAcquisition,
                    Emplacement = m.Emplacement,
                    Statut = m.Statut.ToString(),
                    Notes = m.Notes,
                    NombreInterventions = m.Interventions.Count,
                })
                .ToListAsync();
            return Ok(machines);
        }

        [HttpGet("{id}")]
        [RequireModulePermission("machines", requireWrite: false)]
        public async Task<ActionResult<MachineDetailDto>> GetMachine(int id)
        {
            var machine = await _context.Machines
                .Where(m => m.Id == id)
                .Select(m => new MachineDetailDto
                {
                    Id = m.Id,
                    CodeMachine = m.CodeMachine,
                    Marque = m.Marque,
                    Modele = m.Modele,
                    NumeroSerie = m.NumeroSerie,
                    TypeMachine = m.TypeMachine.ToString(),
                    DateAcquisition = m.DateAcquisition,
                    Emplacement = m.Emplacement,
                    Statut = m.Statut.ToString(),
                    Notes = m.Notes,
                    NombreInterventions = m.Interventions.Count,
                    Interventions = m.Interventions
                        .OrderByDescending(i => i.DateIntervention)
                        .Select(i => new InterventionMachineDto
                        {
                            Id = i.Id,
                            MachineId = i.MachineId,
                            TypeIntervention = i.TypeIntervention.ToString(),
                            DateIntervention = i.DateIntervention,
                            Description = i.Description,
                            PannneConstatee = i.PannneConstatee,
                            PiecesRemplacees = i.PiecesRemplacees,
                            CoutIntervention = i.CoutIntervention,
                            DureeImmobilisationHeures = i.DureeImmobilisationHeures,
                            EffectuePar = i.EffectuePar,
                            ProchaineDateMaintenance = i.ProchaineDateMaintenance,
                            Notes = i.Notes,
                        })
                        .ToList(),
                })
                .FirstOrDefaultAsync();

            if (machine == null)
                return NotFound(new { message = "Machine introuvable." });

            return Ok(machine);
        }

        [HttpPost]
        [RequireModulePermission("machines", requireWrite: true)]
        public async Task<ActionResult> CreateMachine([FromBody] CreateMachineDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CodeMachine))
                return BadRequest(new { message = "Le code machine est requis." });

            if (!Enum.TryParse<TypeMachine>(dto.TypeMachine, out var type))
                return BadRequest(new { message = $"Type de machine invalide : '{dto.TypeMachine}'." });

            if (!Enum.TryParse<StatutMachine>(dto.Statut, out var statut))
                return BadRequest(new { message = $"Statut de machine invalide : '{dto.Statut}'." });

            if (await _context.Machines.AnyAsync(m => m.CodeMachine == dto.CodeMachine))
                return Conflict(new { message = $"Une machine avec le code '{dto.CodeMachine}' existe déjà." });

            var machine = new Machine
            {
                CodeMachine = dto.CodeMachine.Trim(),
                Marque = dto.Marque,
                Modele = dto.Modele,
                NumeroSerie = dto.NumeroSerie,
                TypeMachine = type,
                DateAcquisition = dto.DateAcquisition,
                Emplacement = dto.Emplacement,
                Statut = statut,
                Notes = dto.Notes,
            };
            _context.Machines.Add(machine);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Machine créée", id = machine.Id });
        }

        [HttpPut("{id}")]
        [RequireModulePermission("machines", requireWrite: true)]
        public async Task<ActionResult> UpdateMachine(int id, [FromBody] UpdateMachineDto dto)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null)
                return NotFound(new { message = "Machine introuvable." });

            if (dto.CodeMachine != null)
            {
                if (string.IsNullOrWhiteSpace(dto.CodeMachine))
                    return BadRequest(new { message = "Le code machine est requis." });
                if (await _context.Machines.AnyAsync(m => m.CodeMachine == dto.CodeMachine && m.Id != id))
                    return Conflict(new { message = $"Une machine avec le code '{dto.CodeMachine}' existe déjà." });
                machine.CodeMachine = dto.CodeMachine.Trim();
            }

            if (dto.TypeMachine != null)
            {
                if (!Enum.TryParse<TypeMachine>(dto.TypeMachine, out var type))
                    return BadRequest(new { message = $"Type de machine invalide : '{dto.TypeMachine}'." });
                machine.TypeMachine = type;
            }

            if (dto.Statut != null)
            {
                if (!Enum.TryParse<StatutMachine>(dto.Statut, out var statut))
                    return BadRequest(new { message = $"Statut de machine invalide : '{dto.Statut}'." });
                machine.Statut = statut;
            }

            if (dto.Marque != null) machine.Marque = dto.Marque;
            if (dto.Modele != null) machine.Modele = dto.Modele;
            if (dto.NumeroSerie != null) machine.NumeroSerie = dto.NumeroSerie;
            if (dto.DateAcquisition.HasValue) machine.DateAcquisition = dto.DateAcquisition;
            if (dto.Emplacement != null) machine.Emplacement = dto.Emplacement;
            if (dto.Notes != null) machine.Notes = dto.Notes;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Machine mise à jour" });
        }

        [HttpDelete("{id}")]
        [RequireModulePermission("machines", requireWrite: true)]
        public async Task<ActionResult> DeleteMachine(int id)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null)
                return NotFound(new { message = "Machine introuvable." });

            // Suppression physique : les interventions sont supprimées en cascade
            // (MachineId -> InterventionsMachines, DeleteBehavior.Cascade).
            _context.Machines.Remove(machine);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Machine supprimée" });
        }

        [HttpGet("{id}/Interventions")]
        [RequireModulePermission("machines", requireWrite: false)]
        public async Task<ActionResult<IEnumerable<InterventionMachineDto>>> GetInterventions(int id)
        {
            if (!await _context.Machines.AnyAsync(m => m.Id == id))
                return NotFound(new { message = "Machine introuvable." });

            var interventions = await _context.InterventionsMachines
                .Where(i => i.MachineId == id)
                .OrderByDescending(i => i.DateIntervention)
                .Select(i => new InterventionMachineDto
                {
                    Id = i.Id,
                    MachineId = i.MachineId,
                    TypeIntervention = i.TypeIntervention.ToString(),
                    DateIntervention = i.DateIntervention,
                    Description = i.Description,
                    PannneConstatee = i.PannneConstatee,
                    PiecesRemplacees = i.PiecesRemplacees,
                    CoutIntervention = i.CoutIntervention,
                    DureeImmobilisationHeures = i.DureeImmobilisationHeures,
                    EffectuePar = i.EffectuePar,
                    ProchaineDateMaintenance = i.ProchaineDateMaintenance,
                    Notes = i.Notes,
                })
                .ToListAsync();
            return Ok(interventions);
        }

        [HttpPost("{id}/Interventions")]
        [RequireModulePermission("machines", requireWrite: true)]
        public async Task<ActionResult> CreateIntervention(int id, [FromBody] CreateInterventionMachineDto dto)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null)
                return NotFound(new { message = "Machine introuvable." });

            if (!Enum.TryParse<TypeIntervention>(dto.TypeIntervention, out var type))
                return BadRequest(new { message = $"Type d'intervention invalide : '{dto.TypeIntervention}'." });

            if (string.IsNullOrWhiteSpace(dto.Description))
                return BadRequest(new { message = "La description de l'intervention est requise." });

            var intervention = new InterventionMachine
            {
                MachineId = id,
                TypeIntervention = type,
                DateIntervention = dto.DateIntervention ?? DateTime.Now,
                Description = dto.Description.Trim(),
                PannneConstatee = dto.PannneConstatee,
                PiecesRemplacees = dto.PiecesRemplacees,
                CoutIntervention = dto.CoutIntervention,
                DureeImmobilisationHeures = dto.DureeImmobilisationHeures,
                EffectuePar = dto.EffectuePar,
                ProchaineDateMaintenance = dto.ProchaineDateMaintenance,
                Notes = dto.Notes,
            };
            _context.InterventionsMachines.Add(intervention);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Intervention enregistrée", id = intervention.Id });
        }
    }
}