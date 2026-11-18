using FormPlatform.Application.Abstractions.Tenancy;

namespace FormPlatform.Infrastructure.Tenancy.Development;

/// <summary>Permissive membership reader for Development only.</summary>
public sealed class DevelopmentStubTenantMembershipReader : ITenantMembershipReader
{
	public Task<TenantMembershipResult> ReadMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
	{
		// TODO: Replace with TenantManagement membership checks when that module is available.
		return Task.FromResult(TenantMembershipResult.Member());
	}
}
