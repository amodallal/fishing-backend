using System;

namespace FishingLebanon.Models
{
    /// <summary>
    /// Represents a Guest's reservation for a specific trip.
    /// </summary>
    public class Reservation
    {
        public int Id { get; set; }
        public int NumberOfSeats { get; set; }
        public DateTime ReservationDate { get; set; }

        // Foreign key for the Trip being booked
        public int TripId { get; set; }
        // Navigation property to the Trip

        public string? GuestName { get; set; }
        public string? GuestEmail { get; set; }
        public string? GuestPhone { get; set; }
        public Trip Trip { get; set; }

        // Foreign key for the User (Guest) making the reservation
        public int? UserId { get; set; }
        // Navigation property to the User
        public User User { get; set; }
    }
}
