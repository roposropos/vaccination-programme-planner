using System.Text;
using System.Text.Json.Nodes;

namespace VaxPlan.Api.Fhir;

public class FhirServerOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8090/fhir/";
}

public record FhirExportResult(bool Success, int? StatusCode, int ResourceCount, string Server, string Message);

public class FhirServerClient(HttpClient http, ILogger<FhirServerClient> logger)
{
    public async Task<FhirExportResult> SendTransactionAsync(JsonObject bundle, CancellationToken ct = default)
    {
        var server = http.BaseAddress?.ToString() ?? "(brak adresu)";
        var resourceCount = (bundle["entry"] as JsonArray)?.Count ?? 0;

        try
        {
            using var content = new StringContent(bundle.ToJsonString(), Encoding.UTF8, "application/fhir+json");
            using var response = await http.PostAsync(string.Empty, content, ct);

            if (response.IsSuccessStatusCode)
            {
                return new FhirExportResult(true, (int)response.StatusCode, resourceCount, server, "Dane zostały zapisane na serwerze FHIR.");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Serwer FHIR odrzucił transakcję: {Status} {Body}", (int)response.StatusCode, body);
            return new FhirExportResult(false, (int)response.StatusCode, resourceCount, server, "Serwer FHIR odrzucił dane – szczegóły w logach API.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Brak połączenia z serwerem FHIR {Server}", server);
            return new FhirExportResult(false, null, resourceCount, server, "Brak połączenia z serwerem FHIR (HAPI FHIR startuje ok. 1–2 minuty po uruchomieniu).");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FhirExportResult(false, null, resourceCount, server, "Przekroczono czas oczekiwania na serwer FHIR.");
        }
    }
}
