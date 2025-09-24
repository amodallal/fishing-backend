using System.Collections.Generic;

namespace FishingLebanon.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }

        // --- Authentication Fields ---
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }

        public UserRole Role { get; set; }

        // Navigation properties
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public virtual ICollection<Trip> ManagedTrips { get; set; } = new List<Trip>();
    }
}

