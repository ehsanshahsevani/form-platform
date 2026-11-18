namespace FormPlatform.Application.Abstractions.Tenancy;

public interface ITenantContext
{
	Guid TenantId { get; }
	Guid? UserId { get; }
	bool IsResolved { get; }
	TenantResolutionSource Source { get; }
}

public enum TenantResolutionSource
{
	None = 0,
	DevelopmentHeader = 1,
	AuthenticatedClaim = 2,
	AuthenticatedSession = 3,
	PublicFormToken = 4
}

public interface ITenantResolver
{
	Task<TenantResolutionResult> ResolveAsync(CancellationToken cancellationToken);
}

public interface ITenantMembershipReader
{
	Task<TenantMembershipResult> ReadMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}

public sealed record TenantMembershipResult
{
	private TenantMembershipResult(bool isSuccess, TenantResolutionFailureReason? failureReason, string? description)
	{
		IsSuccess = isSuccess;
		FailureReason = failureReason;
		Description = description;
	}

	public bool IsSuccess { get; }
	public TenantResolutionFailureReason? FailureReason { get; }
	public string? Description { get; }

	public static TenantMembershipResult Member() => new(true, null, null);

	public static TenantMembershipResult Failure(TenantResolutionFailureReason reason, string description)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(description);
		return new(false, reason, description);
	}
}

public sealed record TenantResolutionResult
{
	private TenantResolutionResult(bool isSuccess, Guid? tenantId, Guid? userId,
		TenantResolutionSource source, TenantResolutionFailureReason? failureReason)
	{
		IsSuccess = isSuccess;
		TenantId = tenantId;
		UserId = userId;
		Source = source;
		FailureReason = failureReason;
	}

	public bool IsSuccess { get; }
	public Guid? TenantId { get; }
	public Guid? UserId { get; }
	public TenantResolutionSource Source { get; }
	public TenantResolutionFailureReason? FailureReason { get; }

	public static TenantResolutionResult Success(Guid tenantId, Guid? userId, TenantResolutionSource source)
	{
		if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID must be non-empty.", nameof(tenantId));
		if (source == TenantResolutionSource.None) throw new ArgumentException("A successful result requires a source.", nameof(source));
		return new(true, tenantId, userId, source, null);
	}

	public static TenantResolutionResult Failure(TenantResolutionFailureReason reason) =>
		new(false, null, null, TenantResolutionSource.None, reason);
}

public enum TenantResolutionFailureReason
{
	NotAuthenticated = 0,
	NoTenantClaim = 1,
	TenantNotFound = 2,
	TenantInactive = 3,
	UserNotMember = 4,
	AmbiguousTenant = 5,
	ResolutionNotAvailable = 6
}
