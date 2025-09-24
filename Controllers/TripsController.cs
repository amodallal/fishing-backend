using FishingLebanon.Data;
using FishingLebanon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;

namespace FishingLebanon.Controllers
{
    // --- DTOs for API Requests ---
    public class CreateTripDto
    {
        [Required]
        public string Location { get; set; }
        [Required]
        public System.DateTime Date { get; set; }
        [Range(0.01, 10000)]
        public decimal Price { get; set; }
        [Range(1, 50)]
        public int Capacity { get; set; }
        public int? BoatId { get; set; }

    }

    public class CancelTripDto
    {
        [Required(ErrorMessage = "A cancellation reason is required.")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "The reason must be between 10 and 500 characters.")]
        public string Reason { get; set; }
    }

    public class UpdateTripDto
    {
        [Required]
        public string Location { get; set; }
        [Required]
        public System.DateTime Date { get; set; }
        [Range(0.01, 10000)]
        public decimal Price { get; set; }
        [Range(1, 50)]
        public int Capacity { get; set; }
        public int? BoatId { get; set; }
    }

    // --- DTOs for API Responses (to prevent serialization cycles) ---
    public class BoatDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Capacity { get; set; }
    }

    public class CaptainDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }

        public string Email { get; set; }
    }

    public class TripGuestDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
    }

    public class TripReservationDto
    {
        public int Id { get; set; }
        public int NumberOfSeats { get; set; }
        public TripGuestDto Guest { get; set; }

        // New fields from Reservation table
        public string GuestName { get; set; }
        public string GuestEmail { get; set; }
        public string GuestPhone { get; set; }
    }

    public class TripDto
    {
        public int Id { get; set; }
        public string Location { get; set; }
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
        public int Capacity { get; set; }
        public string Status { get; set; }
        public CaptainDto Captain { get; set; }
        public BoatDto? Boat { get; set; }
        public List<TripReservationDto> Reservations { get; set; }
    }


    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TripsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TripsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Trips
       [HttpGet]
[AllowAnonymous]
public async Task<ActionResult<IEnumerable<TripDto>>> GetTrips([FromQuery] string? location, [FromQuery] DateTime? date)
{
    var query = _context.Trips
        // --- MODIFIED: Added a check to ensure the trip has no reservations ---
        .Where(t => t.Status == TripStatus.Active && !t.Reservations.Any())
        .Include(t => t.Captain)
        .Include(t => t.Boat)
        .AsQueryable();

    // Add this line to filter out trips older than today's date and time
    query = query.Where(t => t.Date >= DateTime.UtcNow); // Use DateTime.UtcNow for consistency with stored dates

    if (!string.IsNullOrWhiteSpace(location))
    {
        query = query.Where(t => t.Location.Contains(location));
    }

    if (date.HasValue)
    {
        // When filtering by a specific date, ensure it's not in the past relative to now
        query = query.Where(t => t.Date.Date == date.Value.Date);
    }

    var trips = await query.Select(t => new TripDto
    {
        Id = t.Id,
        Location = t.Location,
        Date = t.Date,
        Price = t.Price,
        Capacity = t.Capacity,
        Status = t.Status.ToString(),
        Captain = new CaptainDto { Id = t.Captain.Id, FullName = t.Captain.FullName,Email = t.Captain.Email },
        Boat = t.Boat != null ? new BoatDto { Id = t.Boat.Id, Name = t.Boat.Name, Capacity = t.Boat.Capacity } : null,
        Reservations = new List<TripReservationDto>()
    }).ToListAsync();

    return Ok(trips);
}

        // GET: api/Trips/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<TripDto>> GetTrip(int id)
        {
            var trip = await _context.Trips
                .Include(t => t.Captain)
                .Include(t => t.Boat)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
            {
                return NotFound();
            }

            var tripDto = new TripDto
            {
                Id = trip.Id,
                Location = trip.Location,
                Date = trip.Date,
                Price = trip.Price,
                Capacity = trip.Capacity,
                Status = trip.Status.ToString(),
                Captain = new CaptainDto { Id = trip.Captain.Id, FullName = trip.Captain.FullName },
                Boat = trip.Boat != null ? new BoatDto { Id = trip.Boat.Id, Name = trip.Boat.Name, Capacity = trip.Boat.Capacity } : null,
                Reservations = new List<TripReservationDto>()
            };

            return Ok(tripDto);
        }

        // GET: api/Trips/my-trips
        [HttpGet("my-trips")]
        [Authorize(Roles = "Captain")]
        public async Task<ActionResult<IEnumerable<TripDto>>> GetMyTrips()
        {
            var captainId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var trips = await _context.Trips
                .Where(t => t.CaptainId == captainId && t.Status != TripStatus.Cancelled) // exclude canceled trips
                .Include(t => t.Captain)
                .Include(t => t.Boat)
                .Include(t => t.Reservations)
                    .ThenInclude(r => r.User)
                .OrderByDescending(t => t.Date)
                .Select(t => new TripDto
                {
                    Id = t.Id,
                    Location = t.Location ?? "Unknown",
                    Date = t.Date,
                    Price = t.Price,
                    Capacity = t.Capacity,
                    Status = t.Status.ToString(),
                    Captain = t.Captain != null
                        ? new CaptainDto { Id = t.Captain.Id, FullName = t.Captain.FullName ?? "Unnamed Captain" }
                        : null,
                    Boat = t.Boat != null
                        ? new BoatDto { Id = t.Boat.Id, Name = t.Boat.Name ?? "Unnamed Boat", Capacity = t.Boat.Capacity }
                        : null,
                    Reservations = t.Reservations.Select(r => new TripReservationDto
                    {
                        Id = r.Id,
                        NumberOfSeats = r.NumberOfSeats,
                        Guest = r.User != null
                            ? new TripGuestDto { Id = r.User.Id, FullName = r.User.FullName ?? "Anonymous" }
                            : null,
                        GuestName = r.GuestName ?? "N/A",
                        GuestEmail = r.GuestEmail ?? "N/A",
                        GuestPhone = r.GuestPhone ?? "N/A"
                    }).ToList()
                })
                .ToListAsync();

            return Ok(trips);
        }


        // POST: api/Trips
        [HttpPost]
        [Authorize(Roles = "Captain")]
        public async Task<ActionResult<Trip>> CreateTrip(CreateTripDto tripDto)
        {
            var captainId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var trip = new Trip
            {
                Location = tripDto.Location,
                Date = tripDto.Date,
                Price = tripDto.Price,
                Capacity = tripDto.Capacity,
                BoatId = tripDto.BoatId,
                CaptainId = captainId,
                Status = TripStatus.Active
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTrip), new { id = trip.Id }, trip);
        }

        // PUT: api/Trips/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Captain")]
        public async Task<IActionResult> UpdateTrip(int id, UpdateTripDto updateDto)
        {
            var trip = await _context.Trips.FindAsync(id);
            if (trip == null)
            {
                return NotFound();
            }

            var captainId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (trip.CaptainId != captainId)
            {
                return Forbid("You are not authorized to update this trip.");
            }

            trip.Location = updateDto.Location;
            trip.Date = updateDto.Date;
            trip.Price = updateDto.Price;
            trip.Capacity = updateDto.Capacity;
            trip.BoatId = updateDto.BoatId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT: api/Trips/5/cancel
        [HttpPut("{id}/cancel")]
        [Authorize(Roles = "Captain")]
        public async Task<IActionResult> CancelTrip(int id, [FromBody] CancelTripDto dto) // MODIFIED: Added dto
        {
            // MODIFIED: Query now includes reservations to get guest emails
            var trip = await _context.Trips
                .Include(t => t.Reservations)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
                return NotFound();

            var captainId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (trip.CaptainId != captainId)
                return Unauthorized(new { message = "You are not authorized to cancel this trip." });

            if (trip.Status == TripStatus.Cancelled)
                return BadRequest(new { message = "This trip has already been cancelled." });

            trip.Status = TripStatus.Cancelled;
            await _context.SaveChangesAsync();


            // === ADDED: Send notification email to all guests with reservations ===
            try
            {
                // Initialize the SMTP client once, outside the loop for efficiency
                using var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("ahmadmodallal87@gmail.com", "idku ptrx jvqf axpo"),
                    EnableSsl = true
                };

                // Loop through every reservation associated with this trip
                foreach (var reservation in trip.Reservations)
                {
                    if (!string.IsNullOrEmpty(reservation.GuestEmail))
                    {
                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress("ahmadmodallal87@gmail.com", "Fishing Booking System"),
                            Subject = $"Trip Canceled: Your Booking for {trip.Location}",
                            IsBodyHtml = true,
                            Body = $@"
                        <h2>Hello {reservation.GuestName},</h2>
                        <p>We are sorry to inform you that the fishing trip to <b>{trip.Location}</b> scheduled for <b>{trip.Date:MMMM dd, yyyy 'at' HH:mm}</b> has been canceled.</p>
                        <p>The captain provided the following reason for the cancellation:</p>
                        <blockquote style='border-left: 4px solid #ccc; padding-left: 15px; margin-left: 20px;'>
                            <i>""{dto.Reason}""</i>
                        </blockquote>
                        <p>Your reservation has been automatically removed. We apologize for any inconvenience this may cause and hope to see you on another trip soon.</p>
                    "
                        };
                        mailMessage.To.Add(reservation.GuestEmail);
                        await smtpClient.SendMailAsync(mailMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error, but the API call is still a success because the trip was canceled.
                Console.WriteLine($"An error occurred while sending cancellation emails for trip {id}: {ex.Message}");
            }
            // === END of Added Code ===


            return NoContent();
        }
    }
}

