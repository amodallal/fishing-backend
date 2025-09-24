using FishingLebanon.Data;
using FishingLebanon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Added for validation attributes
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FishingLebanon.Controllers
{
    // DTOs (Data Transfer Objects) are used to shape the data sent to and from the API.
    // This is a best practice for security and API clarity.
    // For a larger project, these would be in a separate 'DTOs' folder.

    public class CreateBoatDto
    {
        [Required(ErrorMessage = "Boat name is required.")]
        [StringLength(100)]
        public string Name { get; set; }

        [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
        public int Capacity { get; set; }
    }

    public class UpdateBoatDto
    {
        [Required(ErrorMessage = "Boat name is required.")]
        [StringLength(100)]
        public string Name { get; set; }

        [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
        public int Capacity { get; set; }
    }


    [Route("api/[controller]")]
    [ApiController]
    public class BoatsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // The database context is injected into the controller via the constructor.
        public BoatsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Boats
        // Retrieves all boats from the database.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Boat>>> GetBoats()
        {
            // Asynchronously fetches the list of all boats and returns them.
            return await _context.Boats.ToListAsync();
        }

        // GET: api/Boats/MyBoats
        // Retrieves all boats for the currently authenticated captain.
        [HttpGet("MyBoats")]
        [Authorize(Roles = "Captain")] // Ensures only users with the "Captain" role can access this
        public async Task<ActionResult<IEnumerable<Boat>>> GetMyBoats()
        {
            // 1. Get the logged-in captain's ID from their token claims.
            var captainId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            // 2. Query the database for boats where CaptainId matches the logged-in user's ID.
            var boats = await _context.Boats
                .Where(b => b.CaptainId == captainId)
                .ToListAsync();

            // 3. Return the list of boats.
            return Ok(boats);
        }



        // GET: api/Boats/5
        // Retrieves a specific boat by its ID.
        [HttpGet("{id}")]
        public async Task<ActionResult<Boat>> GetBoat(int id)
        {
            // Finds a boat by its primary key (Id).
            var boat = await _context.Boats.FindAsync(id);

            // If the boat is not found, return a 404 Not Found response.
            if (boat == null)
            {
                return NotFound();
            }

            // If found, return the boat with a 200 OK response.
            return boat;
        }

        // POST: api/Boats
        // Creates a new boat.
        [HttpPost]
        public async Task<ActionResult<Boat>> PostBoat(CreateBoatDto createBoatDto)
        {
            // The [ApiController] attribute automatically checks ModelState.
            // If the model is invalid, it will return a 400 Bad Request response.
            // This check is now implicitly handled.

            // Maps the DTO to the Boat model entity.
            var boat = new Boat
            {
                Name = createBoatDto.Name,
                Capacity = createBoatDto.Capacity
            };

            // Adds the new boat to the context and saves changes to the database.
            _context.Boats.Add(boat);
            await _context.SaveChangesAsync();

            // Returns a 201 Created response with the newly created boat object.
            // The response includes a 'Location' header pointing to the new resource's URL.
            return CreatedAtAction(nameof(GetBoat), new { id = boat.Id }, boat);
        }

        // PUT: api/Boats/5
        // Updates an existing boat.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBoat(int id, UpdateBoatDto updateBoatDto)
        {
            // The [ApiController] attribute automatically handles ModelState validation here as well.

            // Finds the existing boat by its ID.
            var boat = await _context.Boats.FindAsync(id);

            if (boat == null)
            {
                return NotFound(); // Return 404 if the boat doesn't exist.
            }

            // Updates the boat's properties with values from the DTO.
            boat.Name = updateBoatDto.Name;
            boat.Capacity = updateBoatDto.Capacity;

            // Marks the entity as modified.
            _context.Entry(boat).State = EntityState.Modified;

            try
            {
                // Tries to save the changes to the database.
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // If a concurrency conflict occurs (e.g., another user modified the same record),
                // check if the boat still exists.
                if (!_context.Boats.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    // If it still exists, re-throw the exception.
                    throw;
                }
            }

            // Returns a 204 No Content response, indicating success.
            return NoContent();
        }

        // DELETE: api/Boats/5
        // Deletes a boat.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBoat(int id)
        {
            // Finds the boat to be deleted.
            var boat = await _context.Boats.FindAsync(id);
            if (boat == null)
            {
                // If not found, return 404.
                return NotFound();
            }

            // Removes the boat from the context and saves changes.
            _context.Boats.Remove(boat);
            await _context.SaveChangesAsync();

            // Returns a 204 No Content response, indicating success.
            return NoContent();
        }
    }
}

