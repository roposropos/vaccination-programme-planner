using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VaxPlan.Api.Domain;

namespace VaxPlan.Api.Infrastructure;

public class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public static int StatusCodeFor(Exception exception) => exception switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        DomainException => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = StatusCodeFor(exception);
        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Nieobsłużony wyjątek");
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status == StatusCodes.Status500InternalServerError
                    ? "Wystąpił nieoczekiwany błąd serwera."
                    : exception.Message
            }
        });
    }
}
