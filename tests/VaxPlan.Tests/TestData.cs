using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;

namespace VaxPlan.Tests;

// Pins "today" so the tests don't depend on when they run.
public sealed class FixedTimeProvider(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

public static class TestData
{
    public static readonly DateOnly Today = new(2026, 10, 21);

    public static Vaccine Mmr(int id = 1) => new()
    {
        Id = id,
        Code = "MMR",
        Name = "MMR",
        TargetDisease = "Odra, świnka, różyczka",
        CvxCode = "03",
        Icd10Code = "Z27.4",
        Mandatory = true,
        Doses =
        [
            new ScheduleDose { Id = id * 10 + 1, VaccineId = id, DoseNumber = 1, AgeInMonths = 13 },
            new ScheduleDose { Id = id * 10 + 2, VaccineId = id, DoseNumber = 2, AgeInMonths = 72 }
        ]
    };

    public static Patient Patient(int id, DateOnly birthDate) => new()
    {
        Id = id,
        FirstName = "Jan",
        LastName = $"Testowy{id}",
        Pesel = Pesel.Generate(birthDate, Sex.Male, new Random(id)),
        BirthDate = birthDate,
        Sex = Sex.Male,
        GuardianName = "Anna Testowa",
        Email = $"opiekun{id}@example.com"
    };

    public static Immunization Given(Patient patient, Vaccine vaccine, int dose, DateOnly on, int id = 0) => new()
    {
        Id = id,
        PatientId = patient.Id,
        Patient = patient,
        VaccineId = vaccine.Id,
        Vaccine = vaccine,
        DoseNumber = dose,
        AdministeredOn = on,
        LotNumber = "MM1234",
        Status = ImmunizationStatus.Completed,
        RecordedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    public static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
