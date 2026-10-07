using Abadar.Backend.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace Abadar.Backend.Services;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ApiException apiException)
        {
            httpContext.Response.StatusCode = apiException.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(
                new ErrorEnvelope(new ApiError(
                    apiException.Code,
                    apiException.Message,
                    httpContext.TraceIdentifier,
                    apiException.Details)),
                cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled request failure {RequestId}", httpContext.TraceIdentifier);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new ErrorEnvelope(new ApiError(
                "internal_error",
                "An unexpected error occurred.",
                httpContext.TraceIdentifier)),
            cancellationToken);
        return true;
    }
}