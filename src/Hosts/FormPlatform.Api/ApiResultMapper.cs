using FormPlatform.Application.Abstractions;

namespace FormPlatform.Api;

internal static class ApiResultMapper
{
    public static IResult Map<T>(Result<T> result, Func<T, IResult> success)
    {
        if (result.IsSuccess)
        {
            return success(result.Value);
        }

        if (result.Error is not { } error)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred.");
        }

        var statusCode = error.Code switch
        {
            "Forms.NotFound" or "Submissions.NotFound" => StatusCodes.Status404NotFound,
            "Forms.InvalidTransition" or "Forms.ConcurrencyConflict" or "Forms.Conflict" or "Submissions.VersionNotPublished" => StatusCodes.Status409Conflict,
            "Tenant.Required" or "Forms.Invalid" or "Forms.InvalidSchema" or "Submissions.InvalidData" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        var title = statusCode switch
        {
            StatusCodes.Status400BadRequest => "The request is invalid.",
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            StatusCodes.Status409Conflict => "The request conflicts with the current resource state.",
            _ => "An unexpected error occurred."
        };

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: statusCode == StatusCodes.Status500InternalServerError ? null : error.Description);
    }
}
