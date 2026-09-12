using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using modaar.api.Common.Responses;

namespace modaar.api.Common.Responses;

// Wraps every controller response in BaseResponse<T> so the envelope lives in one place instead
// of in every action. Controllers keep returning plain DTOs and ProblemDetails; this turns them
// into the shape the client decodes.
//
// HTTP status codes are left alone. Some envelope designs return 200 for everything and put the
// real outcome in the body — that breaks retries, caching, and every HTTP-aware tool in the
// stack. success:false and a 409 are not in conflict; they are the same fact twice.
public sealed class ResponseEnvelopeFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Anything already wrapped, or not a JSON object result, passes through untouched.
        if (context.Result is not ObjectResult objectResult || IsWrapped(objectResult.Value))
            return next();

        var traceId = context.HttpContext.TraceIdentifier;
        var status = objectResult.StatusCode ?? StatusCodes.Status200OK;

        object envelope = objectResult.Value switch
        {
            ValidationProblemDetails validation => BaseResponse<object>.Fail(new ErrorDto
            {
                Code = status,
                Message = "One or more validation errors occurred.",
                // The per-field map, so the client can attach messages to inputs.
                Details = validation.Errors,
                TraceId = traceId
            }),

            ProblemDetails problem => BaseResponse<object>.Fail(new ErrorDto
            {
                Code = status,
                Message = problem.Detail ?? problem.Title ?? "Request failed.",
                Details = problem.Extensions.TryGetValue("errorCode", out var code) ? code : null,
                TraceId = traceId
            }, unauthorized: status == StatusCodes.Status401Unauthorized),

            var payload => Wrap(payload)
        };

        context.Result = new ObjectResult(envelope) { StatusCode = status };
        return next();
    }

    // Builds BaseResponse<T> with the payload's real type rather than object, so serialization
    // honours any attributes on the DTO.
    private static object Wrap(object? payload)
    {
        if (payload is null)
            return BaseResponse<object>.Ok(null);

        var responseType = typeof(BaseResponse<>).MakeGenericType(payload.GetType());
        var response = Activator.CreateInstance(responseType)!;

        responseType.GetProperty(nameof(BaseResponse<object>.Success))!.SetValue(response, true);
        responseType.GetProperty(nameof(BaseResponse<object>.Result))!.SetValue(response, payload);

        return response;
    }

    private static bool IsWrapped(object? value) =>
        value is not null &&
        value.GetType() is { IsGenericType: true } t &&
        t.GetGenericTypeDefinition() == typeof(BaseResponse<>);
}
