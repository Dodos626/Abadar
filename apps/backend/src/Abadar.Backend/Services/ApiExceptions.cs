namespace Abadar.Backend.Services;

public abstract class ApiException(
    int statusCode,
    string code,
    string message,
    IReadOnlyDictionary<string, string[]>? details = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, string[]>? Details { get; } = details;
}

public sealed class ApiValidationException(IReadOnlyDictionary<string, string[]> details)
    : ApiException(
        StatusCodes.Status400BadRequest,
        "validation_failed",
        "One or more validation errors occurred.",
        details);

public sealed class ApiConflictException(string code, string message)
    : ApiException(StatusCodes.Status409Conflict, code, message);