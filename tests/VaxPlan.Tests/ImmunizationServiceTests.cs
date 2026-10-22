using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Services;

namespace VaxPlan.Tests;

public class ImmunizationServiceTests : IDisposable
{
    private readonly AppDbContext _db = TestData.NewDb();
    private readonly ImmunizationService _service;
    private readonly Patient _patient;
    private readonly Vaccine _mmr;

    public ImmunizationServiceTests()
    {
        _mmr = TestData.Mmr();
        _patient = TestData.Patient(1, TestData.Today.AddYears(-7));
        _db.Vaccines.Add(_mmr);
        _db.Patients.Add(_patient);
        _db.SaveChanges();

        _service = new ImmunizationService(_db, new CalendarService(new FixedTimeProvider(TestData.Today)));
    }

    private static RegisterImmunizationCommand Command(int dose, DateOnly on) =>
        new(VaccineId: 1, DoseNumber: dose, AdministeredOn: on, LotNumber: "MM1234", Site: "ramię", Notes: null);

    [Fact]
    public async Task Registers_first_dose()
    {
        var result = await _service.RegisterAsync(_patient.Id, Command(1, TestData.Today.AddYears(-6)), performedById: null);

        Assert.True(result.Id > 0);
        Assert.Equal(ImmunizationStatus.Completed, result.Status);
        Assert.Single(_db.Immunizations);
    }

    [Fact]
    public async Task Rejects_the_same_dose_twice()
    {
        await _service.RegisterAsync(_patient.Id, Command(1, TestData.Today.AddYears(-6)), null);

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.RegisterAsync(_patient.Id, Command(1, TestData.Today.AddYears(-5)), null));
    }

    [Fact]
    public async Task Rejects_second_dose_without_first()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.RegisterAsync(_patient.Id, Command(2, TestData.Today.AddDays(-1)), null));

        Assert.Contains("dawkę nr 1", ex.Message);
    }

    [Fact]
    public async Task Rejects_second_dose_dated_before_first()
    {
        await _service.RegisterAsync(_patient.Id, Command(1, TestData.Today.AddMonths(-6)), null);

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.RegisterAsync(_patient.Id, Command(2, TestData.Today.AddMonths(-7)), null));
    }

    [Fact]
    public async Task Rejects_date_in_the_future()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.RegisterAsync(_patient.Id, Command(1, TestData.Today.AddDays(1)), null));
    }

    [Fact]
    public async Task Rejects_date_before_birth()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.RegisterAsync(_patient.Id, Command(1, _patient.BirthDate.AddDays(-1)), null));
    }

    [Fact]
    public async Task Rejects_dose_number_outside_schedule()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.RegisterAsync(_patient.Id, Command(3, TestData.Today.AddDays(-1)), null));
    }

    [Fact]
    public async Task Unknown_patient_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RegisterAsync(999, Command(1, TestData.Today.AddDays(-1)), null));
    }

    [Fact]
    public async Task Dose_marked_as_error_can_be_registered_again()
    {
        var wrong = await _service.RegisterAsync(_patient.Id, Command(1, TestData.Today.AddMonths(-6)), null);

        await _service.MarkEnteredInErrorAsync(wrong.Id);
        var corrected = await _service.RegisterAsync(_patient.Id, Command(1, TestData.Today.AddMonths(-5)), null);

        Assert.NotEqual(wrong.Id, corrected.Id);
        Assert.Equal(ImmunizationStatus.EnteredInError, _db.Immunizations.Single(i => i.Id == wrong.Id).Status);
    }

    [Fact]
    public async Task Cannot_cancel_dose_followed_by_later_dose()
    {
        var first = await _service.RegisterAsync(_patient.Id, Command(1, TestData.Today.AddMonths(-6)), null);
        await _service.RegisterAsync(_patient.Id, Command(2, TestData.Today.AddMonths(-1)), null);

        await Assert.ThrowsAsync<DomainException>(() => _service.MarkEnteredInErrorAsync(first.Id));
    }

    public void Dispose() => _db.Dispose();
}
