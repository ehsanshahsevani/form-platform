using FormPlatform.Application.Abstractions;
using FormPlatform.Application.Abstractions.Tenancy;
using FormPlatform.Modules.Forms.Contracts;
using FormPlatform.Modules.Forms.Domain;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace FormPlatform.Modules.Forms.Application;

public interface IFormsRepository
{
	Task AddAsync(Form form, CancellationToken cancellationToken);
	void AddVersion(FormVersion version);
	Task<IReadOnlyList<Form>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
	Task<Form?> GetAsync(Guid id, Guid tenantId, CancellationToken cancellationToken);
	Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ISubmissionRepository
{
	void Add(Submission submission);
	Task<Submission?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken);
	Task<IReadOnlyList<Submission>> ListByFormAsync(Guid formId, Guid tenantId, CancellationToken cancellationToken);
	Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Signals a persistence conflict that callers may resolve by reloading and retrying.</summary>
public sealed class PersistenceConcurrencyException(string message, Exception? innerException = null) : Exception(message, innerException);
public sealed class PersistenceConstraintException(string message, Exception? innerException = null) : Exception(message, innerException);

public sealed class FormsService(IFormsRepository repository, ITenantContext tenant, IClock clock)
{
	public async Task<Result<FormResponse>> CreateAsync(CreateFormRequest request, CancellationToken cancellationToken)
	{
		if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
		{
			return Result<FormResponse>.Failure(new Error("Tenant.Required", "A tenant context is required."));
		}

		Form form;
		try
		{
			form = Form.Create(tenant.TenantId, request.Code, request.Name, request.Description, FormStatus.Draft, clock.UtcNow);
		}
		catch (ArgumentException)
		{
			return Result<FormResponse>.Failure(new Error("Forms.Invalid", "The form data is invalid."));
		}

		await repository.AddAsync(form, cancellationToken);
		try
		{
			await repository.SaveChangesAsync(cancellationToken);
		}
		catch (PersistenceConcurrencyException)
		{
			return Result<FormResponse>.Failure(ConcurrencyError());
		}
		catch (PersistenceConstraintException)
		{
			return Result<FormResponse>.Failure(ConflictError());
		}

		return Result<FormResponse>.Success(Map(form));
	}

	public async Task<Result<FormResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
	{
		if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
		{
			return Result<FormResponse>.Failure(new Error("Tenant.Required", "A tenant context is required."));
		}

		var form = await repository.GetAsync(id, tenant.TenantId, cancellationToken);
		return form is null
			? Result<FormResponse>.Failure(NotFoundError())
			: Result<FormResponse>.Success(Map(form));
	}

	public async Task<Result<IReadOnlyList<FormResponse>>> ListAsync(CancellationToken cancellationToken)
	{
		if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
		{
			return Result<IReadOnlyList<FormResponse>>.Failure(new Error("Tenant.Required", "A tenant context is required."));
		}

		var forms = await repository.ListAsync(tenant.TenantId, cancellationToken);
		return Result<IReadOnlyList<FormResponse>>.Success(forms.Select(Map).ToList());
	}

	public async Task<Result<VersionResponse>> CreateVersionAsync(Guid formId, string? schema, CancellationToken cancellationToken)
	{
		if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
		{
			return Result<VersionResponse>.Failure(new Error("Tenant.Required", "A tenant context is required."));
		}

		var form = await repository.GetAsync(formId, tenant.TenantId, cancellationToken);
		if (form is null)
		{
			return Result<VersionResponse>.Failure(NotFoundError());
		}

		if (schema is not null && !SchemaDocument.IsValid(schema))
		{
			return Result<VersionResponse>.Failure(new Error("Forms.InvalidSchema", "The form schema is invalid."));
		}

		var version = form.CreateDraftVersion(clock.UtcNow);
		if (schema is not null)
		{
			version.UpdateSchema(schema);
		}

		repository.AddVersion(version);
		try
		{
			await repository.SaveChangesAsync(cancellationToken);
		}
		catch (PersistenceConcurrencyException)
		{
			return Result<VersionResponse>.Failure(ConcurrencyError());
		}
		catch (PersistenceConstraintException)
		{
			return Result<VersionResponse>.Failure(ConflictError());
		}

		return Result<VersionResponse>.Success(Map(version));
	}

	public async Task<Result<VersionResponse>> PublishAsync(Guid formId, Guid versionId, CancellationToken cancellationToken)
	{
		if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
		{
			return Result<VersionResponse>.Failure(new Error("Tenant.Required", "A tenant context is required."));
		}

		var form = await repository.GetAsync(formId, tenant.TenantId, cancellationToken);
		var version = form?.Versions.SingleOrDefault(item => item.Id == versionId);
		if (version is null)
		{
			return Result<VersionResponse>.Failure(NotFoundError());
		}

		try
		{
			version.Publish(clock.UtcNow);
			await repository.SaveChangesAsync(cancellationToken);
		}
		catch (InvalidOperationException)
		{
			return Result<VersionResponse>.Failure(new Error("Forms.InvalidTransition", "The version cannot be published in its current state."));
		}
		catch (PersistenceConcurrencyException)
		{
			return Result<VersionResponse>.Failure(ConcurrencyError());
		}

		return Result<VersionResponse>.Success(Map(version));
	}

	private static Error NotFoundError() => new("Forms.NotFound", "The requested form or version was not found.");
	private static Error ConcurrencyError() => new("Forms.ConcurrencyConflict", "The form changed concurrently. Reload and retry.");
	private static Error ConflictError() => new("Forms.Conflict", "A unique form code or version number is already in use.");
	private static FormResponse Map(Form form) => new(form.Id, form.TenantId, form.Code, form.Name, form.Description, form.Status.ToString(), form.CreatedAtUtc, form.UpdatedAtUtc);
	private static VersionResponse Map(FormVersion version) => new(version.Id, version.FormId, version.VersionNumber, version.SchemaVersion, version.Status.ToString(), version.Schema, version.CreatedAtUtc, version.PublishedAtUtc);
}

public sealed class SubmissionManager(
	ISubmissionRepository submissions,
	IFormsRepository forms,
	ITenantContext tenant,
	IClock clock)
{
	public async Task<Result<SubmissionResponse>> CreateAsync(Guid formId, Guid versionId, string dataJson, CancellationToken cancellationToken)
	{
		if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
		{
			return Result<SubmissionResponse>.Failure(new Error("Tenant.Required", "A tenant context is required."));
		}

		var form = await forms.GetAsync(formId, tenant.TenantId, cancellationToken);
		if (form is null)
		{
			return Result<SubmissionResponse>.Failure(new Error("Submissions.NotFound", "The requested form or version was not found."));
		}

		var version = form.Versions.SingleOrDefault(item => item.Id == versionId);
		if (version is null)
		{
			return Result<SubmissionResponse>.Failure(new Error("Submissions.NotFound", "The requested form or version was not found."));
		}

		if (version.Status != FormVersionStatus.Published)
		{
			return Result<SubmissionResponse>.Failure(new Error("Submissions.VersionNotPublished", "Submissions are accepted only for published versions."));
		}

		Submission submission;
		try
		{
			submission = Submission.Create(tenant.TenantId, formId, versionId, dataJson, clock.UtcNow);
		}
		catch (ArgumentException)
		{
			return Result<SubmissionResponse>.Failure(new Error("Submissions.InvalidData", "Submission data must be valid JSON."));
		}

		submissions.Add(submission);
		await submissions.SaveChangesAsync(cancellationToken);
		return Result<SubmissionResponse>.Success(Map(submission));
	}

	public async Task<Result<SubmissionResponse>> GetAsync(Guid submissionId, CancellationToken cancellationToken)
	{
		if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
		{
			return Result<SubmissionResponse>.Failure(new Error("Tenant.Required", "A tenant context is required."));
		}

		var submission = await submissions.GetByIdAsync(submissionId, tenant.TenantId, cancellationToken);
		return submission is null
			? Result<SubmissionResponse>.Failure(new Error("Submissions.NotFound", "The requested submission was not found."))
			: Result<SubmissionResponse>.Success(Map(submission));
	}

	public async Task<Result<IReadOnlyList<SubmissionResponse>>> ListByFormAsync(Guid formId, CancellationToken cancellationToken)
	{
		if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
		{
			return Result<IReadOnlyList<SubmissionResponse>>.Failure(new Error("Tenant.Required", "A tenant context is required."));
		}

		if (await forms.GetAsync(formId, tenant.TenantId, cancellationToken) is null)
		{
			return Result<IReadOnlyList<SubmissionResponse>>.Failure(new Error("Submissions.NotFound", "The requested form was not found."));
		}

		var items = await submissions.ListByFormAsync(formId, tenant.TenantId, cancellationToken);
		return Result<IReadOnlyList<SubmissionResponse>>.Success(items.Select(Map).ToList());
	}

	private static SubmissionResponse Map(Submission submission)
	{
		using var document = JsonDocument.Parse(submission.DataJson);
		return new SubmissionResponse(
			submission.Id,
			submission.TenantId,
			submission.FormId,
			submission.FormVersionId,
			document.RootElement.Clone(),
			submission.CreatedAtUtc);
	}
}

public static class FormsApplicationRegistration
{
	public static IServiceCollection AddFormsApplication(this IServiceCollection services)
	{
		services.AddSingleton<IClock, SystemClock>();
		services.AddScoped<FormsService>();
		services.AddScoped<SubmissionManager>();
		return services;
	}
}
