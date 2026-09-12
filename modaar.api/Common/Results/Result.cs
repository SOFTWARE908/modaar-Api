using Microsoft.AspNetCore.Mvc;

namespace modaar.api.Common.Results;

// AuthErrorCode is auth-shaped and properties is the first slice that doesn't fit it. Same
// pattern as AuthResult<T>, just not tied to sign-in. Migrating the auth slice onto this later
// is a mechanical rename; nothing here forces it.
public enum AppErrorCode
{
    NotFound,
    Forbidden,
    Conflict,
    ValidationFailed
}

public sealed record Result<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public AppErrorCode? ErrorCode { get; init; }
    public string? Error { get; init; }

    public static Result<T> Ok(T value) => new() { Success = true, Value = value };

    public static Result<T> Fail(AppErrorCode code, string error) =>
        new() { Success = false, ErrorCode = code, Error = error };
}

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.Success)
            return new OkObjectResult(result.Value);

        var (status, title) = result.ErrorCode!.Value switch
        {
            AppErrorCode.NotFound         => (StatusCodes.Status404NotFound,   "Not found"),
            AppErrorCode.Forbidden        => (StatusCodes.Status403Forbidden,  "Forbidden"),
            AppErrorCode.Conflict         => (StatusCodes.Status409Conflict,   "Conflict"),
            AppErrorCode.ValidationFailed => (StatusCodes.Status400BadRequest, "Validation failed"),
            _                             => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        var problem = new ProblemDetails { Status = status, Title = title, Detail = result.Error };
        problem.Extensions["errorCode"] = result.ErrorCode.Value.ToString();

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
