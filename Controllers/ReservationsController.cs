using FishingLebanon.Data;
using FishingLebanon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Net;

namespace FishingLebanon.Controllers
{
    // --- DTOs for Reservation API ---

    public class CreateReservationDto
    {
        [Required]
        [Range(1, 50)] // Re-introducing a sensible limit for a single booking
        public int NumberOfSeats { get; set; }
    }




    public class CreateGuestReservationDto  // NO authorization required for this DTO
    {
        [Required]
        [Range(1, 50)]
        public int NumberOfSeats { get; set; }

        [Required]
        public string? GuestName { get; set; }

        [Required, EmailAddress]
        public string? GuestEmail { get; set; }

        public string? GuestPhone { get; set; }
    }

    // New DTO to include full trip details in the reservation response
    public class ReservationTripDetailDto
    {
        public int Id { get; set; }
        public string Location { get; set; }
        public DateTime Date { get; set; }
        public int Capacity { get; set; }

        public Decimal Price { get; set; }
        public string CaptainName { get; set; }
    }

    public class ReservationDto // The main DTO for returning reservation details
    {
        public int Id { get; set; }
        public int NumberOfSeats { get; set; }
        public DateTime ReservationDate { get; set; }
        public ReservationTripDetailDto Trip { get; set; }
    }


    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // All actions in this controller require a logged-in user
    public class ReservationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReservationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/Reservations/trip/{tripId}
        [HttpPost("trip/{tripId}")]
        [Authorize(Roles = "Guest")]
        public async Task<ActionResult<ReservationDto>> CreateReservation(int tripId, CreateReservationDto reservationDto)
        {
            var guestId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var trip = await _context.Trips
                .Include(t => t.Reservations)
                .FirstOrDefaultAsync(t => t.Id == tripId);

            // --- Validation Checks ---
            if (trip == null)
            {
                return NotFound("Trip not found.");
            }
            if (trip.Status != TripStatus.Active)
            {
                return BadRequest("This trip is not active and cannot be booked.");
            }
            if (trip.Date <= DateTime.UtcNow)
            {
                return BadRequest("This trip is in the past and can no longer be booked.");
            }

            // --- UPDATED VALIDATION LOGIC ---
            // Calculate the total number of seats already reserved for this trip.
            var reservedSeats = trip.Reservations.Sum(r => r.NumberOfSeats);
            var availableSeats = trip.Capacity - reservedSeats;

            // Check if there are enough available seats for the new booking request.
            if (reservationDto.NumberOfSeats > availableSeats)
            {
                return BadRequest($"Not enough seats available. Only {availableSeats} seats are left.");
            }

            // --- Create and Save Reservation ---
            var reservation = new Reservation
            {
                TripId = tripId,
                UserId = guestId,
                NumberOfSeats = reservationDto.NumberOfSeats,
                ReservationDate = DateTime.UtcNow
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // We need to load the Captain's data to return it in the DTO
            await _context.Entry(trip).Reference(t => t.Captain).LoadAsync();

            var resultDto = new ReservationDto
            {
                Id = reservation.Id,
                NumberOfSeats = reservation.NumberOfSeats,
                ReservationDate = reservation.ReservationDate,
                Trip = new ReservationTripDetailDto
                {
                    Id = trip.Id,
                    Location = trip.Location,
                    Date = trip.Date,
                    Capacity = trip.Capacity,
                    CaptainName = trip.Captain.FullName
                }
            };

            return CreatedAtAction(nameof(GetMyReservation), new { id = reservation.Id }, resultDto);
        }
        // GET: api/Reservations/my-reservations
        [HttpGet("my-reservations")]
        [Authorize(Roles = "Guest")]
        public async Task<ActionResult<IEnumerable<ReservationDto>>> GetMyReservations()
        {
            var guestId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var reservations = await _context.Reservations
                .Where(r => r.UserId == guestId)
                .Include(r => r.Trip)
                    .ThenInclude(t => t.Captain) // <-- This line correctly includes the captain
                .Select(r => new ReservationDto
                {
                    Id = r.Id,
                    NumberOfSeats = r.NumberOfSeats,
                    ReservationDate = r.ReservationDate,
                    Trip = new ReservationTripDetailDto
                    {
                        Id = r.Trip.Id,
                        Location = r.Trip.Location,
                        Date = r.Trip.Date,
                        Capacity = r.Trip.Capacity,
                        CaptainName = r.Trip.Captain.FullName, // <-- And this line correctly maps the captain's full name
                        Price = r.Trip.Price
                    }
                })
                .ToListAsync();

            return Ok(reservations);
        }

        // A helper endpoint for the CreatedAtAction result
        [HttpGet("{id}")]
        [Authorize(Roles = "Guest")]
        public async Task<ActionResult<ReservationDto>> GetMyReservation(int id)
        {
            var guestId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var reservation = await _context.Reservations
                                        .Where(r => r.UserId == guestId && r.Id == id)
                                        .Include(r => r.Trip)
                                            .ThenInclude(t => t.Captain)
                                        .Select(r => new ReservationDto
                                        {
                                            Id = r.Id,
                                            NumberOfSeats = r.NumberOfSeats,
                                            ReservationDate = r.ReservationDate,
                                            Trip = new ReservationTripDetailDto
                                            {
                                                Id = r.Trip.Id,
                                                Location = r.Trip.Location,
                                                Date = r.Trip.Date,
                                                Capacity = r.Trip.Capacity,
                                                CaptainName = r.Trip.Captain.FullName
                                            }
                                        }).FirstOrDefaultAsync();
            if (reservation == null)
            {
                return NotFound();
            }
            return Ok(reservation);
        }


        // DELETE: api/Reservations/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Guest,Captain")]
        public async Task<IActionResult> CancelReservation(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var reservation = await _context.Reservations
                .Include(r => r.Trip)
                .ThenInclude(t => t.Captain)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            // Authorization checks (unchanged)
            if (userRole == "Guest" && reservation.UserId != userId)
                return Unauthorized(new { message = "You are not authorized to cancel this reservation." });

            if (userRole == "Captain" && reservation.Trip.CaptainId != userId)
                return Unauthorized(new { message = "You can only cancel reservations for your own trips." });

            // === ADDED: Step 1 - Extract info for the email BEFORE deleting ===
            // This ensures we still have the data even after it's removed from the database.
            var guestEmail = reservation.GuestEmail;
            var guestName = reservation.GuestName;
            var tripLocation = reservation.Trip.Location;
            var tripDate = reservation.Trip.Date;

            // Determine who canceled the trip for the email body
            var cancellationSource = userRole == "Captain"
                ? "the trip's captain"
                : "you";
            // === End of Step 1 ===


            // Remove the reservation from the database
            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();


            // === ADDED: Step 2 - Send the cancellation email ===
            if (!string.IsNullOrEmpty(guestEmail))
            {
                try
                {
                    using var smtpClient = new SmtpClient("smtp.gmail.com")
                    {
                        Port = 587,
                        // IMPORTANT: Move credentials to appsettings.json in a real app
                        Credentials = new NetworkCredential("ahmadmodallal87@gmail.com", "idku ptrx jvqf axpo"),
                        EnableSsl = true
                    };

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress("ahmadmodallal87@gmail.com", "Fishing Booking System"),
                        Subject = "Your Fishing Trip Reservation Has Been Canceled",
                        IsBodyHtml = true,
                        Body = $@"
                    <h2>Hello {guestName},</h2>
                    <p>This is a confirmation that your reservation was successfully canceled by <b>{cancellationSource}</b>.</p>
                    <hr>
                    <h3>Canceled Trip Details:</h3>
                    <ul>
                        <li><b>Location:</b> {tripLocation}</li>
                        <li><b>Date:</b> {tripDate:MMMM dd, yyyy 'at' HH:mm}</li>
                    </ul>
                    <p>If you believe this was a mistake, please contact our support.</p>
                "
                    };
                    mailMessage.To.Add(guestEmail);
                    await smtpClient.SendMailAsync(mailMessage);
                }
                catch (Exception ex)
                {
                    // Log the email failure but don't break the API call,
                    // since the cancellation itself was successful.
                    Console.WriteLine($"Failed to send cancellation email to {guestEmail}: {ex.Message}");
                }
            }
            // === End of Step 2 ===

            return NoContent();
        }
        // POST: api/Reservations/guest/trip/{tripId}
        [HttpPost("guest/trip/{tripId}")]
        [AllowAnonymous] // ✅ allow booking without login
        public async Task<ActionResult<ReservationDto>> CreateGuestReservation(int tripId, CreateGuestReservationDto dto)
        {
            var trip = await _context.Trips
                .Include(t => t.Reservations)
                .Include(t => t.Captain) // Captain is already loaded
                .FirstOrDefaultAsync(t => t.Id == tripId);

            if (trip == null)
            {
                return NotFound("Trip not found.");
            }
            if (trip.Status != TripStatus.Active)
            {
                return BadRequest("This trip is not active and cannot be booked.");
            }
            if (trip.Date <= DateTime.UtcNow)
            {
                return BadRequest("This trip is in the past and can no longer be booked.");
            }

            // check available seats
            var reservedSeats = trip.Reservations.Sum(r => r.NumberOfSeats);
            var availableSeats = trip.Capacity - reservedSeats;
            if (dto.NumberOfSeats > availableSeats)
            {
                return BadRequest($"Not enough seats available. Only {availableSeats} seats are left.");
            }

            // save guest reservation (no UserId)
            var reservation = new Reservation
            {
                TripId = tripId,
                NumberOfSeats = dto.NumberOfSeats,
                ReservationDate = DateTime.UtcNow,
                GuestName = dto.GuestName,
                GuestEmail = dto.GuestEmail,
                GuestPhone = dto.GuestPhone
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            try
            {
                using var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("ahmadmodallal87@gmail.com", "idku ptrx jvqf axpo"),
                    EnableSsl = true
                };

                // =========================
                // 1. Send confirmation email to Guest
                // =========================
                var guestMailMessage = new MailMessage
                {
                    From = new MailAddress("ahmadmodallal87@gmail.com", "Fishing Booking system"),
                    Subject = "Fishing Trip Booking Confirmation",
                    IsBodyHtml = true,
                    Body = $@"
                <h2>Hello {reservation.GuestName},</h2>
                <p>Thank you for booking with <b>Fishing Lebanon</b>.</p>
                <p>Your reservation for the trip to <b>{trip.Location}</b> has been confirmed!</p>
                <ul>
                    <li><b>Date:</b> {trip.Date:MMMM dd, yyyy HH:mm}</li>
                    <li><b>Seats Reserved:</b> {reservation.NumberOfSeats}</li>
                    <li><b>Captain:</b> {trip.Captain.FullName}</li>
                </ul>
                <p>We look forward to seeing you!</p>
                <p>Please contact us on 030303030 if you wish to cancel this reservation</p>
            "
                };
                guestMailMessage.To.Add(reservation.GuestEmail);
                await smtpClient.SendMailAsync(guestMailMessage);


                // ==================================================
                // === ADDED: Send Notification Email to Captain ===
                // ==================================================
                // First, check if the captain and their email exist
                if (trip.Captain != null && !string.IsNullOrEmpty(trip.Captain.Email))
                {
                    var captainMailMessage = new MailMessage
                    {
                        From = new MailAddress("ahmadmodallal87@gmail.com", "Fishing Booking System"),
                        Subject = $"New Reservation for your trip to {trip.Location}",
                        IsBodyHtml = true,
                        Body = $@"
                    <h2>Hello Captain {trip.Captain.FullName},</h2>
                    <p>You have received a new reservation for your upcoming trip.</p>
                    <h3>Trip Details:</h3>
                    <ul>
                        <li><b>Location:</b> {trip.Location}</li>
                        <li><b>Date:</b> {trip.Date:MMMM dd, yyyy HH:mm}</li>
                    </ul>
                    <h3>Reservation Details:</h3>
                    <ul>
                        <li><b>Guest Name:</b> {reservation.GuestName}</li>
                        <li><b>Seats Reserved:</b> {reservation.NumberOfSeats}</li>
                        <li><b>Guest Phone:</b> {reservation.GuestPhone}</li>
                        <li><b>Guest Email:</b> {reservation.GuestEmail}</li>
                    </ul>
                    <p>This is an automated notification.</p>
                "
                    };
                    captainMailMessage.To.Add(trip.Captain.Email);
                    await smtpClient.SendMailAsync(captainMailMessage); // Send the second email
                }
                // ==================================================
                // ===            END of Added Code               ===
                // ==================================================

            }
            catch (Exception ex)
            {
                // Optional: log the error but don't block booking
                Console.WriteLine($"Failed to send email: {ex.Message}");
            }

            var resultDto = new ReservationDto
            {
                // ... (rest of the code is unchanged)
            };

            return Ok(resultDto);
        }




    }


}

