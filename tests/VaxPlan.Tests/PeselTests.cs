using VaxPlan.Api.Domain;

namespace VaxPlan.Tests;

public class PeselTests
{
    [Fact]
    public void Accepts_valid_pesel_and_reads_birth_date()
    {
        Assert.True(Pesel.IsValid("44051401359"));
        Assert.True(Pesel.TryGetBirthDate("44051401359", out var birthDate));
        Assert.Equal(new DateOnly(1944, 5, 14), birthDate);
        Assert.Equal(Sex.Male, Pesel.GetSex("44051401359"));
    }

    [Theory]
    [InlineData("44051401358")] // wrong check digit
    [InlineData("4405140135")] // too short
    [InlineData("4405140135a")]
    [InlineData("44133101359")] // month 13
    [InlineData("")]
    public void Rejects_invalid_pesel(string pesel)
    {
        Assert.False(Pesel.IsValid(pesel));
    }

    [Theory]
    [InlineData(2015, 3, 9, Sex.Female)]
    [InlineData(2024, 12, 31, Sex.Male)]
    [InlineData(1999, 7, 1, Sex.Female)]
    public void Generated_pesel_is_valid_and_round_trips(int year, int month, int day, Sex sex)
    {
        var birth = new DateOnly(year, month, day);

        var pesel = Pesel.Generate(birth, sex, new Random(42));

        Assert.True(Pesel.IsValid(pesel));
        Assert.True(Pesel.TryGetBirthDate(pesel, out var decoded));
        Assert.Equal(birth, decoded);
        Assert.Equal(sex, Pesel.GetSex(pesel));
    }

    [Fact]
    public void Births_after_2000_use_month_offset()
    {
        var pesel = Pesel.Generate(new DateOnly(2015, 3, 9), Sex.Female, new Random(1));

        Assert.Equal("1523", pesel[..4]);
    }
}
