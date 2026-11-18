using System.Text.Json;
using FormPlatform.Domain.Abstractions;

namespace FormPlatform.Modules.Forms.Domain;

public sealed class Submission : AggregateRoot<Guid>
{
    private Submission(Guid id, Guid tenantId, Guid formId, Guid formVersionId, string dataJson, DateTime createdAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        FormId = formId;
        FormVersionId = formVersionId;
        DataJson = dataJson;
        CreatedAtUtc = createdAtUtc;
    }

    private Submission() : base(Guid.Empty)
    {
        DataJson = string.Empty;
    }

    public Guid TenantId { get; private set; }
    public Guid FormId { get; private set; }
    public Guid FormVersionId { get; private set; }
    public string DataJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Submission Create(Guid tenantId, Guid formId, Guid formVersionId, string? dataJson, DateTime createdAtUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A valid tenant is required.", nameof(tenantId));
        if (formId == Guid.Empty) throw new ArgumentException("A valid form is required.", nameof(formId));
        if (formVersionId == Guid.Empty) throw new ArgumentException("A valid form version is required.", nameof(formVersionId));
        if (string.IsNullOrWhiteSpace(dataJson)) throw new ArgumentException("Submission data is required.", nameof(dataJson));

        try
        {
            using var _ = JsonDocument.Parse(dataJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Submission data must be valid JSON.", nameof(dataJson), exception);
        }

        return new Submission(Guid.NewGuid(), tenantId, formId, formVersionId, dataJson, createdAtUtc);
    }
}
