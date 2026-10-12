using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core (code-first) context. Table, column, key and constraint names are configured to
/// match docs/RaceDay_Database_Schema.sql and docs/ERD_RaceDay.png.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventEnrolment> EventEnrolments => Set<EventEnrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.RoleId);
            e.Property(r => r.RoleId).HasColumnName("RoleID");
            e.Property(r => r.RoleName).HasMaxLength(50).IsRequired();
            e.HasIndex(r => r.RoleName).IsUnique().HasDatabaseName("UQ_Roles_RoleName");

            e.HasData(
                new Role { RoleId = AppRoles.OrganiserId, RoleName = AppRoles.Organiser },
                new Role { RoleId = AppRoles.ParticipantId, RoleName = AppRoles.Participant },
                new Role { RoleId = AppRoles.AdminId, RoleName = AppRoles.Admin });
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.UserId);
            e.Property(u => u.UserId).HasColumnName("UserID");
            e.Property(u => u.RoleId).HasColumnName("RoleID");
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(150).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
            e.Property(u => u.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_Users_Email");

            e.HasOne(u => u.Role).WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .HasConstraintName("FK_Users_Roles")
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events", t => t.HasCheckConstraint("CK_Events_EventType", "EventType IN ('Run', 'Walk', 'Cycle')"));
            e.HasKey(ev => ev.EventId);
            e.Property(ev => ev.EventId).HasColumnName("EventID");
            e.Property(ev => ev.OrganiserId).HasColumnName("OrganiserID");
            e.Property(ev => ev.EventName).HasMaxLength(150).IsRequired();
            e.Property(ev => ev.EventDate).HasColumnType("date");
            e.Property(ev => ev.Location).HasMaxLength(200).IsRequired();
            e.Property(ev => ev.Description).HasColumnType("nvarchar(max)");
            e.Property(ev => ev.DistanceKm).HasColumnType("decimal(6,2)");
            e.Property(ev => ev.EventType).HasConversion<string>().HasMaxLength(10);
            e.Property(ev => ev.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");

            e.HasOne(ev => ev.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(ev => ev.OrganiserId)
                .HasConstraintName("FK_Events_Users")
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(c => c.CategoryId);
            e.Property(c => c.CategoryId).HasColumnName("CategoryID");
            e.Property(c => c.EventId).HasColumnName("EventID");
            e.Property(c => c.CategoryName).HasMaxLength(100).IsRequired();
            e.Property(c => c.DistanceKm).HasColumnType("decimal(5,2)");
            e.Property(c => c.MaxParticipants).HasDefaultValue(100);
            e.Property(c => c.EntryFee).HasColumnType("decimal(8,2)").HasDefaultValue(0m);

            // Deleting an event removes its categories (and, through them, enrolments and results).
            e.HasOne(c => c.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(c => c.EventId)
                .HasConstraintName("FK_Categories_Events")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventEnrolment>(e =>
        {
            e.ToTable("EventEnrolments");
            e.HasKey(en => en.EnrolmentId);
            e.Property(en => en.EnrolmentId).HasColumnName("EnrolmentID");
            e.Property(en => en.ParticipantId).HasColumnName("ParticipantID");
            e.Property(en => en.CategoryId).HasColumnName("CategoryID");
            e.Property(en => en.BibNumber).HasMaxLength(10);
            e.Property(en => en.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.Property(en => en.Status).HasMaxLength(20).HasDefaultValue("Confirmed");
            e.HasIndex(en => new { en.ParticipantId, en.CategoryId }).IsUnique()
                .HasDatabaseName("UQ_Enrolment_Participant_Category");

            e.HasOne(en => en.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(en => en.ParticipantId)
                .HasConstraintName("FK_Enrolments_Users")
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(en => en.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(en => en.CategoryId)
                .HasConstraintName("FK_Enrolments_Categories")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Results");
            e.HasKey(r => r.ResultId);
            e.Property(r => r.ResultId).HasColumnName("ResultID");
            e.Property(r => r.EnrolmentId).HasColumnName("EnrolmentID");
            e.Property(r => r.FinishTime).HasColumnType("time");
            e.Property(r => r.Status).HasMaxLength(20).HasDefaultValue("Pending");
            e.HasIndex(r => r.EnrolmentId).IsUnique().HasDatabaseName("UQ_Results_Enrolment");

            e.HasOne(r => r.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId)
                .HasConstraintName("FK_Results_Enrolments")
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
