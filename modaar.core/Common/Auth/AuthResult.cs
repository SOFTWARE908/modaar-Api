namespace modaar.api.Common.Auth;

public sealed record AuthResult<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public AuthErrorCode? ErrorCode { get; init; }
    public string? Error { get; init; }

    public static AuthResult<T> Ok(T value) => new() { Success = true, Value = value };

    public static AuthResult<T> Fail(AuthErrorCode code, string error) =>
        new() { Success = false, ErrorCode = code, Error = error };
}
