using System.Security.Claims;
using VaxPlan.Api.Services;

namespace VaxPlan.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(AppClaims.UserId)?.Value, out var id) ? id : null;

    public static int? GetPatientId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(AppClaims.PatientId)?.Value, out var id) ? id : null;
}
