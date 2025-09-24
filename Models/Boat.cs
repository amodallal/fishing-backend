using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FishingLebanon.Models
{
    public class Boat
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Capacity { get; set; }

        // --- New Properties ---
        // Foreign key to the User who owns this boat
        public int CaptainId { get; set; }

        // Navigation property to the User (Captain)
        [ForeignKey("CaptainId")]
        public User Captain { get; set; }
        // --- End of New Properties ---

        public ICollection<Trip> Trips { get; set; } = new List<Trip>();
    }
}

