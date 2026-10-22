using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VaxPlan.Api.Domain;

namespace VaxPlan.Api.Data;

public class DataSeeder(
    AppDbContext db,
    IPasswordHasher<AppUser> passwordHasher,
    TimeProvider clock,
    ILogger<DataSeeder> logger)
{
    public const string DemoPassword = "Demo123!";

    public async Task SeedAsync(int patientCount, int seed = 2026, CancellationToken ct = default)
    {
        if (await db.Vaccines.AnyAsync(ct))
        {
            return;
        }

        var random = new Random(seed);
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        var vaccines = VaccineCatalog.Create();
        db.Vaccines.AddRange(vaccines);

        var admin = CreateUser("admin@vaxplan.local", "Tomasz Lis", Roles.Admin);
        var nurse = CreateUser("pielegniarka@vaxplan.local", "Beata Wrona", Roles.Nurse);
        var doctor = CreateUser("lekarz@vaxplan.local", "lek. Marek Sobczak", Roles.Doctor);
        db.Users.AddRange(admin, nurse, doctor);
        await db.SaveChangesAsync(ct);

        var patients = GeneratePatients(patientCount, seed, today, random);
        db.Patients.AddRange(patients);
        await db.SaveChangesAsync(ct);

        foreach (var patient in patients)
        {
            // Most families keep to the schedule; roughly a quarter fall behind.
            var lagging = random.NextDouble() < 0.25;
            var compliance = lagging ? 0.6 + random.NextDouble() * 0.3 : 0.995;

            foreach (var vaccine in vaccines)
            {
                var lastDate = (DateOnly?)null;
                foreach (var dose in vaccine.Doses.OrderBy(d => d.DoseNumber))
                {
                    var dueDate = patient.BirthDate.AddMonths(dose.AgeInMonths);
                    if (dueDate > today || random.NextDouble() > compliance)
                    {
                        break; // a missed dose means the later ones in the series are missing too
                    }

                    var given = dueDate.AddDays(random.Next(0, 25));
                    if (lastDate is { } previous && given <= previous)
                    {
                        given = previous.AddDays(30);
                    }

                    if (given > today)
                    {
                        given = today;
                    }

                    if (lastDate is { } last && given <= last)
                    {
                        break;
                    }

                    db.Immunizations.Add(new Immunization
                    {
                        PatientId = patient.Id,
                        VaccineId = vaccine.Id,
                        DoseNumber = dose.DoseNumber,
                        AdministeredOn = given,
                        LotNumber = $"{vaccine.Code[..2]}{random.Next(1000, 9999)}",
                        Site = dose.AgeInMonths < 12 ? "udo" : "ramię",
                        PerformedById = nurse.Id,
                        RecordedAt = DateTime.UtcNow
                    });
                    lastDate = given;
                }
            }
        }

        // The parent demo account gets the youngest child, whose schedule has the most still ahead.
        var demoPatient = patients.OrderByDescending(p => p.BirthDate).First();
        db.Users.Add(CreateUser("pacjent@vaxplan.local", demoPatient.GuardianName ?? demoPatient.FullName, Roles.Patient, demoPatient.Id));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Zasiano bazę: {Vaccines} szczepionek, {Patients} pacjentów.", vaccines.Count, patients.Count);
    }

    private List<Patient> GeneratePatients(int count, int seed, DateOnly today, Random random)
    {
        var usedPesels = new HashSet<string>();
        var faker = new Faker<Patient>("pl")
            .UseSeed(seed)
            .RuleFor(p => p.Sex, f => f.PickRandom<Sex>())
            .RuleFor(p => p.FirstName, (f, p) => f.PickRandom(p.Sex == Sex.Male ? BoyNames : GirlNames))
            .RuleFor(p => p.LastName, (f, p) => Surname(f.PickRandom(Surnames), p.Sex))
            .RuleFor(p => p.BirthDate, f => today.AddDays(-f.Random.Int(10, 17 * 365)))
            .RuleFor(p => p.GuardianName, (f, p) =>
            {
                var mother = f.Random.Bool(0.7f);
                var name = f.PickRandom(mother ? WomenNames : MenNames);
                return $"{name} {Surname(Masculine(p.LastName), mother ? Sex.Female : Sex.Male)}";
            })
            .RuleFor(p => p.Email, f => $"opiekun{f.IndexFaker + 1}@example.com")
            .RuleFor(p => p.Phone, f => f.Phone.PhoneNumber("5## ### ###"))
            .RuleFor(p => p.CreatedAt, _ => DateTime.UtcNow);

        var patients = faker.Generate(count);
        foreach (var patient in patients)
        {
            string pesel;
            do
            {
                pesel = Pesel.Generate(patient.BirthDate, patient.Sex, random);
            }
            while (!usedPesels.Add(pesel));

            patient.Pesel = pesel;
        }

        return patients;
    }

    // Bogus's Polish locale ignores gender for first names, so the names live here.
    private static readonly string[] BoyNames =
    [
        "Antoni", "Jan", "Aleksander", "Franciszek", "Nikodem", "Jakub", "Szymon", "Filip", "Leon", "Stanisław",
        "Mikołaj", "Wojciech", "Kacper", "Adam", "Tymon", "Michał", "Ignacy", "Wiktor", "Piotr", "Marcel"
    ];

    private static readonly string[] GirlNames =
    [
        "Zofia", "Zuzanna", "Hanna", "Julia", "Maja", "Laura", "Oliwia", "Alicja", "Pola", "Lena",
        "Maria", "Amelia", "Natalia", "Wiktoria", "Antonina", "Emilia", "Helena", "Nadia", "Aleksandra", "Gabriela"
    ];

    private static readonly string[] MenNames =
        ["Tomasz", "Paweł", "Marcin", "Krzysztof", "Łukasz", "Grzegorz", "Piotr", "Michał", "Rafał", "Bartosz"];

    private static readonly string[] WomenNames =
        ["Anna", "Katarzyna", "Agnieszka", "Magdalena", "Joanna", "Monika", "Karolina", "Ewa", "Marta", "Justyna"];

    private static readonly string[] Surnames =
    [
        "Nowak", "Kowalski", "Wiśniewski", "Wójcik", "Kowalczyk", "Kamiński", "Lewandowski", "Zieliński", "Szymański",
        "Woźniak", "Dąbrowski", "Kozłowski", "Jankowski", "Mazur", "Kwiatkowski", "Krawczyk", "Piotrowski", "Grabowski",
        "Nowakowski", "Pawłowski", "Michalski", "Adamczyk", "Dudek", "Zając", "Wieczorek", "Jabłoński", "Król",
        "Majewski", "Olszewski", "Jaworski", "Malinowski", "Stępień", "Górski", "Rutkowski", "Witkowski", "Walczak",
        "Sikora", "Baran", "Szewczyk", "Ostrowski", "Sadowski", "Zawadzki", "Wróblewski", "Chmielewski", "Borkowski"
    ];

    private static string Surname(string masculine, Sex sex) =>
        sex == Sex.Female && masculine.EndsWith('i') && (masculine.EndsWith("ski") || masculine.EndsWith("cki") || masculine.EndsWith("dzki"))
            ? masculine[..^1] + "a"
            : masculine;

    private static string Masculine(string surname) =>
        surname.EndsWith("ska") || surname.EndsWith("cka") || surname.EndsWith("dzka") ? surname[..^1] + "i" : surname;

    private AppUser CreateUser(string email, string displayName, string role, int? patientId = null)
    {
        var user = new AppUser
        {
            Email = email,
            DisplayName = displayName,
            Role = role,
            PatientId = patientId,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, DemoPassword);
        return user;
    }
}
