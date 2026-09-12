using Microsoft.AspNetCore.Diagnostics;
using modaar.api.Common.Responses;

namespace modaar.api.Common.Errors;

// Rewritten to emit BaseResponse rather than a bare ProblemDetails: an unhandled exception must
// not be the one response shape the client cannot parse.
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception for {Method} {Path} ({TraceId})",
            httpContext.Request.Method, httpContext.Request.Path, httpContext.TraceIdentifier);

        var response = BaseResponse<object>.Fail(new ErrorDto
        {
            Code = StatusCodes.Status500InternalServerError,
            // The trace id goes to the user so a support ticket can be tied to the log entry.
            Message = _env.IsDevelopment() ? exception.Message : "Please try again later.",
            Details = _env.IsDevelopment() ? exception.StackTrace : null,
            TraceId = httpContext.TraceIdentifier
        });

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}

// The JWT middleware challenges before any filter runs, so a 401 or 403 would otherwise go back
// with an empty body — the one case the client cannot decode.
public sealed class AuthChallengeEnvelopeMiddleware
{
    private readonly RequestDelegate _next;

    public AuthChallengeEnvelopeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            var isChallenge = context.Response.StatusCode is
                StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden;

            context.Response.Body = originalBody;

            // Only step in when the pipeline produced nothing; a filter-built body is left alone.
            if (isChallenge && buffer.Length == 0)
            {
                var unauthorized = context.Response.StatusCode == StatusCodes.Status401Unauthorized;

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(BaseResponse<object>.Fail(new ErrorDto
                {
                    Code = context.Response.StatusCode,
                    Message = unauthorized ? "Authentication is required." : "You do not have access to this resource.",
                    Details = unauthorized ? "Unauthorized" : "Forbidden",
                    TraceId = context.TraceIdentifier
                }, unauthorized));

                return;
            }

            buffer.Seek(0, SeekOrigin.Begin);
            await buffer.CopyToAsync(originalBody);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}