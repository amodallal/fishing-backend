using Microsoft.EntityFrameworkCore;
using FishingLebanon.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace FishingLebanon.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Boat> Boats { get; set; }
        public DbSet<Trip> Trips { get; set; }
        public DbSet<Reservation> Reservations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the UserRole enum to be stored as a string (e.g., "Captain") in the database
            // This makes the database more readable than storing a number.
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            // Define the one-to-many relationship between a User (as a Captain) and Trip
            modelBuilder.Entity<User>()
                .HasMany(u => u.ManagedTrips)
                .WithOne(t => t.Captain)
                .HasForeignKey(t => t.CaptainId)
                .OnDelete(DeleteBehavior.Restrict); // Important: Prevents deleting a captain if they still have trips

            // Define the one-to-many relationship between a User (as a Guest) and Reservation
            modelBuilder.Entity<User>()
                .HasMany(u => u.Reservations)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade); // If a user is deleted, their reservations are also deleted

            modelBuilder.Entity<Trip>()
                .Property(t => t.Status)
                .HasConversion<string>();

        }
    }
}
