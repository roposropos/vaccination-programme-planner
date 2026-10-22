using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Fhir;

namespace VaxPlan.Api.Controllers;

[ApiController]
[Route("fhir")]
[Authorize(Roles = Roles.Clinical)]
public class FhirController(FhirPatientService patients, FhirMapper mapper, FhirServerClient server) : ControllerBase
{
    private const string FhirJson = "application/fhir+json";

    [HttpGet("Patient/{id:int}")]
    public async Task<IActionResult> GetPatient(int id, CancellationToken ct)
    {
        var patient = await patients.LoadPatientAsync(id, ct);
        return Fhir(mapper.ToPatient(patient));
    }

    /// <summary>Immunization search: /fhir/Immunization?patient=1</summary>
    [HttpGet("Immunization")]
    public async Task<IActionResult> SearchImmunizations([FromQuery] int patient, CancellationToken ct)
    {
        var loaded = await patients.LoadPatientAsync(patient, ct);
        var resources = loaded.Immunizations.OrderBy(i => i.AdministeredOn).Select(mapper.ToImmunization);
        return Fhir(mapper.ToSearchBundle(resources, BaseUrl));
    }

    /// <summary>Vaccination plan: /fhir/ImmunizationRecommendation?patient=1</summary>
    [HttpGet("ImmunizationRecommendation")]
    public async Task<IActionResult> SearchRecommendations([FromQuery] int patient, CancellationToken ct)
    {
        var loaded = await patients.LoadPatientAsync(patient, ct);
        var recommendation = await patients.GetRecommendationAsync(loaded, ct);
        return Fhir(mapper.ToSearchBundle([recommendation], BaseUrl));
    }

    /// <summary>All resources of one patient in a single Bundle.</summary>
    [HttpGet("Patient/{id:int}/$everything")]
    public async Task<IActionResult> Everything(int id, CancellationToken ct)
    {
        var resources = await patients.GetAllResourcesAsync(id, ct);
        return Fhir(mapper.ToSearchBundle(resources, BaseUrl));
    }

    /// <summary>Sends the patient's resources to the HAPI FHIR server as a transaction Bundle.</summary>
    [HttpPost("/api/fhir/export/{patientId:int}")]
    public async Task<ActionResult<FhirExportResult>> Export(int patientId, CancellationToken ct)
    {
        var resources = await patients.GetAllResourcesAsync(patientId, ct);
        var result = await server.SendTransactionAsync(mapper.ToTransactionBundle(resources), ct);
        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status502BadGateway, result);
        }

        return result;
    }

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}/fhir";

    private ContentResult Fhir(JsonObject resource) => Content(resource.ToJsonString(), FhirJson);
}
