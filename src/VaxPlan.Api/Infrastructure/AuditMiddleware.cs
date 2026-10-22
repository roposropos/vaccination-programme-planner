using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Services;

namespace VaxPlan.Api.Infrastructure;

public class AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var audited = !HttpMethods.IsOptions(context.Request.Method)
                      && (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
                          || path.StartsWith("/fhir", StringComparison.OrdinalIgnoreCase));

        if (!audited)
        {
            await next(context);
            return;
        }

        int? failedStatus = null;
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            failedStatus = ApiExceptionHandler.StatusCodeFor(ex);
            throw;
        }
        finally
        {
            await WriteAsync(context, db, path, failedStatus ?? context.Response.StatusCode);
        }
    }

    private async Task WriteAsync(HttpContext context, AppDbContext db, string path, int statusCode)
    {
        try
        {
            // Don't let the audit write save anything the request left half-finished.
            db.ChangeTracker.Clear();

            var fullPath = path + context.Request.QueryString.Value;
            db.AuditEntries.Add(new AuditEntry
            {
                Timestamp = DateTime.UtcNow,
                UserId = context.User.GetUserId(),
                UserEmail = context.User.FindFirst(AppClaims.Email)?.Value,
                Role = context.User.FindFirst(AppClaims.Role)?.Value,
                Method = context.Request.Method,
                Path = fullPath.Length > 500 ? fullPath[..500] : fullPath,
                StatusCode = statusCode,
                IpAddress = ClientIp(context)
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nie udało się zapisać wpisu audytu dla {Path}", path);
        }
    }

    private static string? ClientIp(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        return ip is { IsIPv4MappedToIPv6: true } ? ip.MapToIPv4().ToString() : ip?.ToString();
    }
}
