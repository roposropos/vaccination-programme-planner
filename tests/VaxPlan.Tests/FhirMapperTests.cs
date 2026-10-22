using System.Text.Json.Nodes;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Fhir;
using VaxPlan.Api.Services;

namespace VaxPlan.Tests;

public class FhirMapperTests
{
    private readonly FhirMapper _mapper = new();
    private readonly Vaccine _mmr = TestData.Mmr();

    [Fact]
    public void Patient_contains_pesel_identifier_gender_and_birth_date()
    {
        var patient = TestData.Patient(7, new DateOnly(2020, 2, 29));

        var resource = _mapper.ToPatient(patient);

        Assert.Equal("Patient", Str(resource["resourceType"]));
        Assert.Equal("pat-7", Str(resource["id"]));
        Assert.Equal(FhirMapper.PeselSystem, Str(resource["identifier"]![0]!["system"]));
        Assert.Equal(patient.Pesel, Str(resource["identifier"]![0]!["value"]));
        Assert.Equal("male", Str(resource["gender"]));
        Assert.Equal("2020-02-29", Str(resource["birthDate"]));
    }

    [Fact]
    public void Immunization_uses_cvx_code_icd10_reason_and_dose_number()
    {
        var patient = TestData.Patient(1, new DateOnly(2020, 1, 1));
        var immunization = TestData.Given(patient, _mmr, 2, new DateOnly(2026, 1, 10), id: 3);

        var resource = _mapper.ToImmunization(immunization);

        Assert.Equal("completed", Str(resource["status"]));
        Assert.Equal(FhirMapper.CvxSystem, Str(resource["vaccineCode"]!["coding"]![0]!["system"]));
        Assert.Equal("03", Str(resource["vaccineCode"]!["coding"]![0]!["code"]));
        Assert.Equal("Z27.4", Str(resource["reasonCode"]![0]!["coding"]![0]!["code"]));
        Assert.Equal("Patient/pat-1", Str(resource["patient"]!["reference"]));
        Assert.Equal("2026-01-10", Str(resource["occurrenceDateTime"]));
        Assert.Equal(2, resource["protocolApplied"]![0]!["doseNumberPositiveInt"]!.GetValue<int>());
        Assert.Equal(2, resource["protocolApplied"]![0]!["seriesDosesPositiveInt"]!.GetValue<int>());
    }

    [Fact]
    public void Cancelled_immunization_has_entered_in_error_status()
    {
        var patient = TestData.Patient(1, new DateOnly(2020, 1, 1));
        var immunization = TestData.Given(patient, _mmr, 1, new DateOnly(2021, 3, 1));
        immunization.Status = ImmunizationStatus.EnteredInError;

        Assert.Equal("entered-in-error", Str(_mapper.ToImmunization(immunization)["status"]));
    }

    [Fact]
    public void Recommendation_lists_overdue_and_upcoming_doses()
    {
        var patient = TestData.Patient(1, TestData.Today.AddMonths(-20));
        var calendar = new CalendarService(new FixedTimeProvider(TestData.Today)).Build(patient, [_mmr], []);

        var resource = _mapper.ToRecommendation(calendar, [_mmr]);
        var statuses = resource["recommendation"]!.AsArray()
            .Select(r => Str(r!["forecastStatus"]!["coding"]![0]!["code"]))
            .ToList();

        Assert.Equal("ImmunizationRecommendation", Str(resource["resourceType"]));
        Assert.Equal(new[] { "overdue", "due" }, statuses);
    }

    [Fact]
    public void Transaction_bundle_uses_put_with_resource_ids()
    {
        var patient = TestData.Patient(4, new DateOnly(2020, 1, 1));

        var bundle = _mapper.ToTransactionBundle([_mapper.ToPatient(patient)]);
        var entry = bundle["entry"]![0]!;

        Assert.Equal("transaction", Str(bundle["type"]));
        Assert.Equal("PUT", Str(entry["request"]!["method"]));
        Assert.Equal("Patient/pat-4", Str(entry["request"]!["url"]));
    }

    private static string? Str(JsonNode? node) => node?.GetValue<string>();
}
