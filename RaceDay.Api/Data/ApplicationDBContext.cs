namespace RaceDay.Api.Data;

public class ApplicationDBContext : DbContext
{
    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Organiser> Organisers => Set<Organiser>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    // Mirrors the Part 1 SQL script: same tables, keys, constraints and delete rules.
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser','Participant')"));
            e.HasKey(x => x.UserId);
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.Property(x => x.CreatedDate).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Organiser).WithOne(o => o.User).HasForeignKey<Organiser>(o => o.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Participant).WithOne(p => p.User).HasForeignKey<Participant>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Organiser>(e =>
        {
            e.ToTable("Organisers");
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.ContactNumber).HasMaxLength(20);
            e.Property(x => x.OrganisationName).HasMaxLength(150);
        });

        b.Entity<Participant>(e =>
        {
            e.ToTable("Participants");
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.ContactNumber).HasMaxLength(20);
            e.Property(x => x.ProfilePictureUrl).HasMaxLength(500);
        });

        b.Entity<Event>(e =>
        {
            e.ToTable("Events", t => t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run','Walk','Cycle')"));
            e.HasKey(x => x.EventId);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Location).HasMaxLength(150).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.EventType).HasMaxLength(10).IsRequired();
            e.Property(x => x.BannerImageUrl).HasMaxLength(500);
            e.HasOne(x => x.Organiser).WithMany(o => o.Events).HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending','Confirmed','Cancelled')"));
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            e.Property(x => x.EnrolmentDate).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique().HasDatabaseName("UQ_Enrolments_ParticipantEvent");
            e.HasOne(x => x.Participant).WithMany(p => p.Enrolments).HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Result>(e =>
        {
            e.ToTable("Results");
            e.HasKey(x => x.ResultId);
            e.Property(x => x.CapturedDate).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.EnrolmentId).IsUnique();
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result).HasForeignKey<Result>(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
