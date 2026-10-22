using System.Globalization;
using System.Text.Json.Nodes;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Services;

namespace VaxPlan.Api.Fhir;

public class FhirMapper
{
    public const string CvxSystem = "http://hl7.org/fhir/sid/cvx";
    public const string Icd10System = "http://hl7.org/fhir/sid/icd-10";
    public const string LoincSystem = "http://loinc.org";
    public const string RecommendationStatusSystem = "http://terminology.hl7.org/CodeSystem/immunization-recommendation-status";

    // PESEL identifier system used by the Polish FHIR profiles.
    public const string PeselSystem = "urn:oid:2.16.840.1.113883.3.4424.1.1.616";

    public static string PatientId(int id) => $"pat-{id}";
    public static string ImmunizationId(int id) => $"imm-{id}";
    public static string PractitionerId(int id) => $"prac-{id}";
    public static string RecommendationId(int patientId) => $"rec-{patientId}";

    public JsonObject ToPatient(Patient patient)
    {
        var resource = new JsonObject
        {
            ["resourceType"] = "Patient",
            ["id"] = PatientId(patient.Id),
            ["identifier"] = new JsonArray
            {
                new JsonObject { ["system"] = PeselSystem, ["value"] = patient.Pesel }
            },
            ["name"] = new JsonArray
            {
                new JsonObject
                {
                    ["use"] = "official",
                    ["family"] = patient.LastName,
                    ["given"] = new JsonArray { patient.FirstName }
                }
            },
            ["gender"] = patient.Sex == Sex.Male ? "male" : "female",
            ["birthDate"] = FormatDate(patient.BirthDate)
        };

        var telecom = new JsonArray();
        if (!string.IsNullOrWhiteSpace(patient.Phone))
        {
            telecom.Add(new JsonObject { ["system"] = "phone", ["value"] = patient.Phone });
        }

        if (!string.IsNullOrWhiteSpace(patient.Email))
        {
            telecom.Add(new JsonObject { ["system"] = "email", ["value"] = patient.Email });
        }

        if (telecom.Count > 0)
        {
            resource["telecom"] = telecom;
        }

        if (!string.IsNullOrWhiteSpace(patient.GuardianName))
        {
            resource["contact"] = new JsonArray
            {
                new JsonObject
                {
                    ["relationship"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["coding"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["system"] = "http://terminology.hl7.org/CodeSystem/v2-0131",
                                    ["code"] = "N",
                                    ["display"] = "Next-of-Kin"
                                }
                            },
                            ["text"] = "Opiekun prawny"
                        }
                    },
                    ["name"] = new JsonObject { ["text"] = patient.GuardianName }
                }
            };
        }

        return resource;
    }

    public JsonObject ToPractitioner(AppUser user) => new()
    {
        ["resourceType"] = "Practitioner",
        ["id"] = PractitionerId(user.Id),
        ["name"] = new JsonArray { new JsonObject { ["text"] = user.DisplayName } },
        ["telecom"] = new JsonArray { new JsonObject { ["system"] = "email", ["value"] = user.Email } }
    };

    public JsonObject ToImmunization(Immunization immunization)
    {
        var vaccine = immunization.Vaccine
            ?? throw new InvalidOperationException("Do mapowania potrzebna jest załadowana szczepionka (Include).");

        var resource = new JsonObject
        {
            ["resourceType"] = "Immunization",
            ["id"] = ImmunizationId(immunization.Id),
            ["status"] = immunization.Status == ImmunizationStatus.Completed ? "completed" : "entered-in-error",
            ["vaccineCode"] = VaccineConcept(vaccine),
            ["patient"] = new JsonObject { ["reference"] = $"Patient/{PatientId(immunization.PatientId)}" },
            ["occurrenceDateTime"] = FormatDate(immunization.AdministeredOn),
            ["recorded"] = immunization.RecordedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["primarySource"] = true,
            ["lotNumber"] = immunization.LotNumber,
            ["reasonCode"] = new JsonArray
            {
                new JsonObject
                {
                    ["coding"] = new JsonArray
                    {
                        new JsonObject { ["system"] = Icd10System, ["code"] = vaccine.Icd10Code }
                    }
                }
            },
            ["protocolApplied"] = new JsonArray
            {
                new JsonObject
                {
                    ["targetDisease"] = new JsonArray { new JsonObject { ["text"] = vaccine.TargetDisease } },
                    ["doseNumberPositiveInt"] = immunization.DoseNumber,
                    ["seriesDosesPositiveInt"] = Math.Max(vaccine.Doses.Count, immunization.DoseNumber)
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(immunization.Site))
        {
            resource["site"] = new JsonObject { ["text"] = immunization.Site };
        }

        if (immunization.PerformedBy is { } performer)
        {
            resource["performer"] = new JsonArray
            {
                new JsonObject
                {
                    ["actor"] = new JsonObject
                    {
                        ["reference"] = $"Practitioner/{PractitionerId(performer.Id)}",
                        ["display"] = performer.DisplayName
                    }
                }
            };
        }

        if (!string.IsNullOrWhiteSpace(immunization.Notes))
        {
            resource["note"] = new JsonArray { new JsonObject { ["text"] = immunization.Notes } };
        }

        return resource;
    }

    public JsonObject ToRecommendation(PatientCalendar calendar, IReadOnlyCollection<Vaccine> vaccines)
    {
        var recommendations = new JsonArray();

        foreach (var entry in calendar.Entries.Where(e => e.Status != DoseStatus.Completed))
        {
            var vaccine = vaccines.First(v => v.Id == entry.VaccineId);
            recommendations.Add(new JsonObject
            {
                ["vaccineCode"] = new JsonArray { VaccineConcept(vaccine) },
                ["targetDisease"] = new JsonObject { ["text"] = vaccine.TargetDisease },
                ["forecastStatus"] = ForecastStatus(entry.Status == DoseStatus.Overdue ? "overdue" : "due"),
                ["dateCriterion"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["code"] = new JsonObject
                        {
                            ["coding"] = new JsonArray
                            {
                                new JsonObject { ["system"] = LoincSystem, ["code"] = "30980-7", ["display"] = "Date vaccine due" }
                            }
                        },
                        ["value"] = FormatDate(entry.DueDate)
                    }
                },
                ["doseNumberPositiveInt"] = entry.DoseNumber,
                ["seriesDosesPositiveInt"] = entry.SeriesDoses
            });
        }

        var completedVaccineIds = calendar.Entries
            .GroupBy(e => e.VaccineId)
            .Where(g => g.All(e => e.Status == DoseStatus.Completed))
            .Select(g => g.Key);

        foreach (var vaccineId in completedVaccineIds)
        {
            var vaccine = vaccines.First(v => v.Id == vaccineId);
            recommendations.Add(new JsonObject
            {
                ["vaccineCode"] = new JsonArray { VaccineConcept(vaccine) },
                ["targetDisease"] = new JsonObject { ["text"] = vaccine.TargetDisease },
                ["forecastStatus"] = ForecastStatus("complete")
            });
        }

        return new JsonObject
        {
            ["resourceType"] = "ImmunizationRecommendation",
            ["id"] = RecommendationId(calendar.PatientId),
            ["patient"] = new JsonObject { ["reference"] = $"Patient/{PatientId(calendar.PatientId)}" },
            ["date"] = FormatDate(calendar.Today),
            ["recommendation"] = recommendations
        };
    }

    public JsonObject ToSearchBundle(IEnumerable<JsonObject> resources, string baseUrl)
    {
        var entries = new JsonArray();
        foreach (var resource in resources)
        {
            entries.Add(new JsonObject
            {
                ["fullUrl"] = $"{baseUrl.TrimEnd('/')}/{ResourceUrl(resource)}",
                ["resource"] = resource.DeepClone(),
                ["search"] = new JsonObject { ["mode"] = "match" }
            });
        }

        return new JsonObject
        {
            ["resourceType"] = "Bundle",
            ["type"] = "searchset",
            ["total"] = entries.Count,
            ["entry"] = entries
        };
    }

    // PUT with our own ids makes a repeated export update the same resources instead of adding copies.
    public JsonObject ToTransactionBundle(IEnumerable<JsonObject> resources)
    {
        var entries = new JsonArray();
        foreach (var resource in resources)
        {
            entries.Add(new JsonObject
            {
                ["resource"] = resource.DeepClone(),
                ["request"] = new JsonObject { ["method"] = "PUT", ["url"] = ResourceUrl(resource) }
            });
        }

        return new JsonObject
        {
            ["resourceType"] = "Bundle",
            ["type"] = "transaction",
            ["entry"] = entries
        };
    }

    public static string ResourceUrl(JsonObject resource) =>
        $"{resource["resourceType"]!.GetValue<string>()}/{resource["id"]!.GetValue<string>()}";

    private static JsonObject VaccineConcept(Vaccine vaccine) => new()
    {
        ["coding"] = new JsonArray
        {
            new JsonObject { ["system"] = CvxSystem, ["code"] = vaccine.CvxCode, ["display"] = vaccine.Name }
        },
        ["text"] = vaccine.Name
    };

    private static JsonObject ForecastStatus(string code) => new()
    {
        ["coding"] = new JsonArray
        {
            new JsonObject { ["system"] = RecommendationStatusSystem, ["code"] = code }
        }
    };

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
