namespace modaar.api.Common.Responses;

// The envelope the Flutter client decodes. The payload key must stay "result" — the client reads
// data['result'] and renaming it to "value" or "data" breaks every screen at once.
public record BaseResponse<T>
{
    public required bool Success { get; init; }

    public T? Result { get; init; }

    public ErrorDto? Error { get; init; }

    // Distinct from a 401 status so the client can drop to the login screen from a body it has
    // already parsed, without special-casing the transport layer.
    public bool UnAuthorizedRequest { get; init; }

    public static BaseResponse<T> Ok(T? result) => new() { Success = true, Result = result };

    public static BaseResponse<T> Fail(ErrorDto error, bool unauthorized = false) =>
        new() { Success = false, Error = error, UnAuthorizedRequest = unauthorized };
}

public record ErrorDto
{
    // The HTTP status, kept in the body so a client reading only the payload still knows.
    public int Code { get; init; }

    // Safe to show a user.
    public required string Message { get; init; }

    // Machine-readable discriminator — AppErrorCode or AuthErrorCode as a string, or the
    // validation error map. Never an exception message.
    public object? Details { get; init; }

    // Correlates with the Serilog entry, so a support ticket can be traced to a request.
    public string? TraceId { get; init; }
}
