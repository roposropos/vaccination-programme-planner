using VaxPlan.Api.Domain;
using VaxPlan.Api.Services;

namespace VaxPlan.Tests;

public class CalendarServiceTests
{
    private readonly CalendarService _calendar = new(new FixedTimeProvider(TestData.Today));
    private readonly Vaccine _mmr = TestData.Mmr();

    [Fact]
    public void Given_dose_is_completed()
    {
        var patient = TestData.Patient(1, TestData.Today.AddMonths(-20));
        var immunization = TestData.Given(patient, _mmr, 1, TestData.Today.AddMonths(-7), id: 5);

        var entries = _calendar.BuildEntries(patient, [_mmr], [immunization]);

        var first = entries.Single(e => e.DoseNumber == 1);
        Assert.Equal(DoseStatus.Completed, first.Status);
        Assert.Equal(5, first.ImmunizationId);
    }

    [Fact]
    public void Missed_dose_after_due_date_is_overdue()
    {
        var patient = TestData.Patient(1, TestData.Today.AddMonths(-20)); // MMR dose 1 was due 7 months ago

        var entries = _calendar.BuildEntries(patient, [_mmr], []);

        Assert.Equal(DoseStatus.Overdue, entries.Single(e => e.DoseNumber == 1).Status);
    }

    [Fact]
    public void Dose_due_within_window_is_due_soon()
    {
        var patient = TestData.Patient(1, TestData.Today.AddMonths(-13).AddDays(10)); // due in about 10 days

        var entries = _calendar.BuildEntries(patient, [_mmr], []);

        Assert.Equal(DoseStatus.DueSoon, entries.Single(e => e.DoseNumber == 1).Status);
    }

    [Fact]
    public void Dose_far_in_future_is_planned()
    {
        var patient = TestData.Patient(1, TestData.Today.AddMonths(-20));

        var entries = _calendar.BuildEntries(patient, [_mmr], []);

        Assert.Equal(DoseStatus.Planned, entries.Single(e => e.DoseNumber == 2).Status);
    }

    [Fact]
    public void Entry_marked_entered_in_error_does_not_count_as_given()
    {
        var patient = TestData.Patient(1, TestData.Today.AddMonths(-20));
        var wrong = TestData.Given(patient, _mmr, 1, TestData.Today.AddMonths(-7));
        wrong.Status = ImmunizationStatus.EnteredInError;

        var entries = _calendar.BuildEntries(patient, [_mmr], [wrong]);

        Assert.Equal(DoseStatus.Overdue, entries.Single(e => e.DoseNumber == 1).Status);
    }

    [Fact]
    public void Due_date_is_birth_date_plus_age_in_months()
    {
        var birth = new DateOnly(2024, 1, 15);
        var patient = TestData.Patient(1, birth);

        var entries = _calendar.BuildEntries(patient, [_mmr], []);

        Assert.Equal(new DateOnly(2025, 2, 15), entries.Single(e => e.DoseNumber == 1).DueDate);
        Assert.Equal(new DateOnly(2030, 1, 15), entries.Single(e => e.DoseNumber == 2).DueDate);
    }

    [Fact]
    public void Build_summarises_statuses()
    {
        var patient = TestData.Patient(1, TestData.Today.AddMonths(-20));

        var calendar = _calendar.Build(patient, [_mmr], []);

        Assert.Equal(0, calendar.Completed);
        Assert.Equal(1, calendar.Overdue);
        Assert.Equal(1, calendar.Planned);
        Assert.Equal(2, calendar.Entries.Count);
    }

    [Theory]
    [InlineData(-1, DoseStatus.Overdue)]
    [InlineData(0, DoseStatus.DueSoon)]
    [InlineData(CalendarService.DueSoonWindowDays, DoseStatus.DueSoon)]
    [InlineData(CalendarService.DueSoonWindowDays + 1, DoseStatus.Planned)]
    public void Status_boundaries(int daysFromToday, DoseStatus expected)
    {
        Assert.Equal(expected, CalendarService.ResolveStatus(TestData.Today.AddDays(daysFromToday), TestData.Today));
    }
}
