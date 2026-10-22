using VaxPlan.Api.Services;

namespace VaxPlan.Tests;

public class ReportServiceTests
{
    [Fact]
    public void Coverage_counts_only_doses_that_are_already_due()
    {
        var mmr = TestData.Mmr();
        var vaccinated = TestData.Patient(1, TestData.Today.AddMonths(-20));
        var notVaccinated = TestData.Patient(2, TestData.Today.AddMonths(-20));
        var baby = TestData.Patient(3, TestData.Today.AddMonths(-2)); // too young for MMR
        vaccinated.Immunizations.Add(TestData.Given(vaccinated, mmr, 1, TestData.Today.AddMonths(-6)));

        var clock = new FixedTimeProvider(TestData.Today);
        var service = new ReportService(TestData.NewDb(), new CalendarService(clock));

        var report = service.BuildSummary([vaccinated, notVaccinated, baby], [mmr], TestData.Today.AddYears(-1), TestData.Today);

        var dose1 = Assert.Single(report.Coverage);
        Assert.Equal(2, dose1.Eligible);
        Assert.Equal(1, dose1.Completed);
        Assert.Equal(50.0, dose1.CoveragePercent);

        Assert.Equal(3, report.Patients);
        Assert.Equal(1, report.PatientsWithOverdue);
        Assert.Equal(2, report.PatientsUpToDate);
        Assert.Equal(1, report.ImmunizationsInPeriod);
    }
}
