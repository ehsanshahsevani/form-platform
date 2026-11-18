using FormPlatform.Domain.Abstractions;

namespace FormPlatform.Modules.Forms.Domain;

public enum FormStatus
{
    Draft,
    Active,
    Archived
}

public enum FormVersionStatus
{
    Draft,
    Published,
    Retired
}

public sealed class Form : AggregateRoot<Guid>
{
    private readonly List<FormVersion> _versions = [];

    private Form(Guid id, Guid tenantId, string code, string name, string? description, FormStatus status, DateTime now) : base(id)
    {
        TenantId = tenantId;
        Code = code;
        Name = name;
        Description = description;
        Status = status;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    private Form() : base(Guid.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public Guid TenantId { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public FormStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<FormVersion> Versions => _versions.AsReadOnly();

    public static Form Create(Guid tenantId, string? code, string? name, string? description, FormStatus status, DateTime now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A valid tenant is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code cannot be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new Form(Guid.NewGuid(), tenantId, code.Trim(), name.Trim(), description?.Trim(), status, now);
    }

    public FormVersion CreateDraftVersion(DateTime now)
    {
        var nextVersionNumber = _versions.Count == 0 ? 1 : _versions.Max(version => version.VersionNumber) + 1;
        var version = FormVersion.Create(Id, TenantId, nextVersionNumber, now);
        _versions.Add(version);
        UpdatedAtUtc = now;
        return version;
    }
}

public sealed class FormVersion : Entity<Guid>
{
    private FormVersion(Guid id, Guid formId, Guid tenantId, int number, DateTime now) : base(id)
    {
        FormId = formId;
        TenantId = tenantId;
        VersionNumber = number;
        SchemaVersion = 1;
        Status = FormVersionStatus.Draft;
        Schema = "{\"metadata\":{},\"fields\":[]}";
        CreatedAtUtc = now;
    }

    private FormVersion() : base(Guid.Empty)
    {
        Schema = string.Empty;
    }

    public Guid FormId { get; private set; }
    public Guid TenantId { get; private set; }
    public int VersionNumber { get; private set; }
    public int SchemaVersion { get; private set; }
    public FormVersionStatus Status { get; private set; }
    public string Schema { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    internal static FormVersion Create(Guid formId, Guid tenantId, int number, DateTime now) => new(Guid.NewGuid(), formId, tenantId, number, now);

    public void UpdateSchema(string schema)
    {
        if (Status != FormVersionStatus.Draft)
        {
            throw new InvalidOperationException("Only draft versions can be modified.");
        }

        if (!SchemaDocument.IsValid(schema))
        {
            throw new ArgumentException("The schema is invalid.", nameof(schema));
        }

        Schema = schema;
    }

    public void Publish(DateTime now)
    {
        if (Status != FormVersionStatus.Draft)
        {
            throw new InvalidOperationException("Only draft versions can be published.");
        }

        if (!SchemaDocument.IsValid(Schema))
        {
            throw new InvalidOperationException("The schema is invalid.");
        }

        Status = FormVersionStatus.Published;
        PublishedAtUtc = now;
    }

    public void Retire()
    {
        if (Status != FormVersionStatus.Published)
        {
            throw new InvalidOperationException("Only published versions can be retired.");
        }

        Status = FormVersionStatus.Retired;
    }
}
