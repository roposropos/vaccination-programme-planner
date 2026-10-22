using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Domain;

namespace VaxPlan.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Vaccine> Vaccines => Set<Vaccine>();
    public DbSet<ScheduleDose> ScheduleDoses => Set<ScheduleDose>();
    public DbSet<Immunization> Immunizations => Set<Immunization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Patient>(e =>
        {
            e.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
            e.Property(p => p.LastName).HasMaxLength(100).IsRequired();
            e.Property(p => p.Pesel).HasMaxLength(11).IsRequired();
            e.Property(p => p.Email).HasMaxLength(200);
            e.Property(p => p.Phone).HasMaxLength(30);
            e.Property(p => p.GuardianName).HasMaxLength(200);
            e.Property(p => p.Sex).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(p => p.Pesel).IsUnique();
            e.HasIndex(p => p.LastName);
        });

        modelBuilder.Entity<Vaccine>(e =>
        {
            e.Property(v => v.Code).HasMaxLength(20).IsRequired();
            e.Property(v => v.Name).HasMaxLength(200).IsRequired();
            e.Property(v => v.TargetDisease).HasMaxLength(200);
            e.Property(v => v.CvxCode).HasMaxLength(10);
            e.Property(v => v.Icd10Code).HasMaxLength(10);
            e.HasIndex(v => v.Code).IsUnique();
            e.HasMany(v => v.Doses)
                .WithOne(d => d.Vaccine)
                .HasForeignKey(d => d.VaccineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScheduleDose>(e =>
        {
            e.Property(d => d.Description).HasMaxLength(300);
            e.HasIndex(d => new { d.VaccineId, d.DoseNumber }).IsUnique();
        });

        modelBuilder.Entity<Immunization>(e =>
        {
            e.Property(i => i.LotNumber).HasMaxLength(50).IsRequired();
            e.Property(i => i.Site).HasMaxLength(100);
            e.Property(i => i.Notes).HasMaxLength(1000);
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(i => i.Patient)
                .WithMany(p => p.Immunizations)
                .HasForeignKey(i => i.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Vaccine)
                .WithMany()
                .HasForeignKey(i => i.VaccineId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.PerformedBy)
                .WithMany()
                .HasForeignKey(i => i.PerformedById)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(i => new { i.PatientId, i.VaccineId, i.DoseNumber });
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Patient)
                .WithMany()
                .HasForeignKey(u => u.PatientId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.Property(a => a.Method).HasMaxLength(10);
            e.Property(a => a.Path).HasMaxLength(500);
            e.Property(a => a.UserEmail).HasMaxLength(200);
            e.Property(a => a.Role).HasMaxLength(20);
            e.Property(a => a.IpAddress).HasMaxLength(50);
            e.HasIndex(a => a.Timestamp);
        });
    }
}

public static class AppDbContextExtensions
{
    public static Task<List<Vaccine>> LoadVaccineScheduleAsync(this AppDbContext db, CancellationToken ct = default) =>
        db.Vaccines
            .AsNoTracking()
            .Include(v => v.Doses)
            .OrderBy(v => v.Id)
            .ToListAsync(ct);
}
