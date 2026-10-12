using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace backend;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> LegacyUsers { get; set; }
    public DbSet<AvailableDate> AvailableDates { get; set; }
    public DbSet<UnavailableDate> UnavailableDates { get; set; }
    public DbSet<Reservation> Reservations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<AvailableDate>().HasKey(date => date.Date);
        modelBuilder.Entity<UnavailableDate>().HasKey(date => date.Date);
        modelBuilder.Entity<Reservation>()
            .HasIndex(reservation => new { reservation.ReservationDate, reservation.ReservationTime })
            .IsUnique()
            .HasFilter("\"Status\" != 'Rejected'");
        modelBuilder.Entity<Reservation>()
            .HasIndex(reservation => new { reservation.ReservationDate, reservation.Status });
    }
}

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class AvailableDate
{
    public string Date { get; set; } = string.Empty;
}

public class UnavailableDate
{
    public string Date { get; set; } = string.Empty;
}

public class Reservation
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string ReservationDate { get; set; } = string.Empty;
    public string ReservationTime { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string LocationAddress { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public int TravelFeeCents { get; set; }
    public string Status { get; set; } = "Pending";
    public string RejectionReason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
