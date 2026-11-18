using FormPlatform.Api;
using FormPlatform.Modules.Forms.Application;
using FormPlatform.Modules.Forms.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FormPlatform.Api.Endpoints;

internal static class FormsEndpoints
{
	public static IEndpointRouteBuilder MapFormsEndpoints(this IEndpointRouteBuilder endpoints)
	{
		var forms = endpoints.MapTenantGroup("/api/v1/forms");

		forms.MapPost("", async ([FromBody] CreateFormRequest request, FormsService service, CancellationToken cancellationToken) =>
			ApiResultMapper.Map(await service.CreateAsync(request, cancellationToken),
				form => Results.Created($"/api/v1/forms/{form.Id}", form)));

		forms.MapGet("", async (FormsService service, CancellationToken cancellationToken) =>
			ApiResultMapper.Map(await service.ListAsync(cancellationToken), Results.Ok));

		forms.MapGet("/{formId:guid}", async (Guid formId, FormsService service, CancellationToken cancellationToken) =>
			ApiResultMapper.Map(await service.GetAsync(formId, cancellationToken), Results.Ok));

		forms.MapPost("/{formId:guid}/versions", async (Guid formId, [FromBody] CreateVersionRequest request, FormsService service, CancellationToken cancellationToken) =>
			ApiResultMapper.Map(await service.CreateVersionAsync(formId, request.Schema, cancellationToken),
				version => Results.Created($"/api/v1/forms/{formId}/versions/{version.Id}", version)));

		forms.MapPost("/{formId:guid}/versions/{versionId:guid}/publish", async (Guid formId, Guid versionId, FormsService service, CancellationToken cancellationToken) =>
			ApiResultMapper.Map(await service.PublishAsync(formId, versionId, cancellationToken), Results.Ok));

		forms.MapGet("/{formId:guid}/submissions", async (Guid formId, SubmissionManager manager, CancellationToken cancellationToken) =>
			ApiResultMapper.Map(await manager.ListByFormAsync(formId, cancellationToken), Results.Ok));

		return endpoints;
	}
}
