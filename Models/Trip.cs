using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FishingLebanon.Models
{
    /// <summary>
    /// Represents a scheduled fishing trip, created by a Captain.
    /// </summary>
    public class Trip
    {
        public int Id { get; set; }
        public string Location { get; set; }
        public DateTime Date { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        public int Capacity { get; set; }

        // Foreign key for the Boat
        public int? BoatId { get; set; }
        // Navigation property to the Boat
        public Boat? Boat { get; set; }

        // Foreign key for the Captain (who is a User)
        public int CaptainId { get; set; }
        // Navigation property to the User who is the Captain
        public User Captain { get; set; }

        public TripStatus Status { get; set; } = TripStatus.Active;

        // A collection of reservations for this trip
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}

