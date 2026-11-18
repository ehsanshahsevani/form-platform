using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using FormPlatform.Modules.Forms.Contracts;
using FormPlatform.Modules.Forms.Application;

namespace FormPlatform.Api.Endpoints;

internal static class RuntimeEndpoints
{
	public static IEndpointRouteBuilder MapRuntimeEndpoints(this IEndpointRouteBuilder endpoints)
	{
		var runtime = endpoints.MapTenantGroup("/api/v1/runtime");

		runtime.MapPost("/forms/{formId:guid}/versions/{versionId:guid}/submissions", async (
			Guid formId,
			Guid versionId,
			[FromBody] CreateSubmissionRequest request,
			SubmissionManager manager,
			CancellationToken cancellationToken) =>
		{
			if (request.Data is not JsonElement data || data.ValueKind == JsonValueKind.Undefined)
			{
				return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Submission data is required.");
			}

			return ApiResultMapper.Map(await manager.CreateAsync(formId, versionId, data.GetRawText(), cancellationToken),
				submission => Results.Created($"/api/v1/runtime/submissions/{submission.Id}", submission));
		});

		runtime.MapGet("/submissions/{submissionId:guid}", async (Guid submissionId, SubmissionManager manager, CancellationToken cancellationToken) =>
			ApiResultMapper.Map(await manager.GetAsync(submissionId, cancellationToken), Results.Ok));

		return endpoints;
	}
}
