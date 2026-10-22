namespace VaxPlan.Api.Domain;

public static class Pesel
{
    private static readonly int[] Weights = [1, 3, 7, 9, 1, 3, 7, 9, 1, 3];

    public static bool IsValid(string? pesel)
    {
        if (pesel is null || pesel.Length != 11 || !pesel.All(char.IsAsciiDigit))
        {
            return false;
        }

        return ControlDigit(pesel[..10]) == pesel[10] - '0' && TryGetBirthDate(pesel, out _);
    }

    public static bool TryGetBirthDate(string pesel, out DateOnly birthDate)
    {
        birthDate = default;
        if (pesel.Length < 6 || !pesel[..6].All(char.IsAsciiDigit))
        {
            return false;
        }

        var yy = int.Parse(pesel[..2]);
        var mm = int.Parse(pesel.Substring(2, 2));
        var dd = int.Parse(pesel.Substring(4, 2));

        int century;
        switch (mm)
        {
            case > 80: century = 1800; mm -= 80; break;
            case > 60: century = 2200; mm -= 60; break;
            case > 40: century = 2100; mm -= 40; break;
            case > 20: century = 2000; mm -= 20; break;
            default: century = 1900; break;
        }

        if (mm is < 1 or > 12)
        {
            return false;
        }

        var year = century + yy;
        if (dd < 1 || dd > DateTime.DaysInMonth(year, mm))
        {
            return false;
        }

        birthDate = new DateOnly(year, mm, dd);
        return true;
    }

    public static Sex GetSex(string pesel) => (pesel[9] - '0') % 2 == 0 ? Sex.Female : Sex.Male;

    public static string Generate(DateOnly birthDate, Sex sex, Random random)
    {
        var monthOffset = birthDate.Year switch
        {
            >= 1800 and < 1900 => 80,
            >= 2000 and < 2100 => 20,
            >= 2100 and < 2200 => 40,
            _ => 0
        };

        var serial = random.Next(0, 1000);
        var sexDigit = random.Next(0, 5) * 2 + (sex == Sex.Male ? 1 : 0);
        var core = $"{birthDate.Year % 100:00}{birthDate.Month + monthOffset:00}{birthDate.Day:00}{serial:000}{sexDigit}";
        return core + ControlDigit(core);
    }

    private static int ControlDigit(string first10)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            sum += (first10[i] - '0') * Weights[i];
        }

        return (10 - sum % 10) % 10;
    }
}
