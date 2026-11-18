using System.Text.Json;

namespace FormPlatform.Modules.Forms.Contracts;
public sealed record CreateFormRequest(string Code, string Name, string? Description);
public sealed record FormResponse(Guid Id, Guid TenantId, string Code, string Name, string? Description, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record CreateVersionRequest(string? Schema);
public sealed record VersionResponse(Guid Id, Guid FormId, int VersionNumber, int SchemaVersion, string Status, string Schema, DateTime CreatedAtUtc, DateTime? PublishedAtUtc);
public sealed record CreateSubmissionRequest(JsonElement? Data);
public sealed record SubmissionResponse(Guid Id, Guid TenantId, Guid FormId, Guid FormVersionId, JsonElement Data, DateTime CreatedAtUtc);
