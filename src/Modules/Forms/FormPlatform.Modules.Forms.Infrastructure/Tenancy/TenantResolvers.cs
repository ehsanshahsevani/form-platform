using System.Security.Claims;
using FormPlatform.Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Http;

namespace FormPlatform.Infrastructure.Tenancy;

public sealed class DevelopmentHeaderTenantResolver(IHttpContextAccessor accessor) : ITenantResolver
{
	public Task<TenantResolutionResult> ResolveAsync(CancellationToken cancellationToken)
	{
		var request = accessor.HttpContext?.Request;
		if (request is null || !Guid.TryParse(request.Headers["X-Tenant-Id"].FirstOrDefault(), out var tenantId) || tenantId == Guid.Empty)
		{
			return Task.FromResult(TenantResolutionResult.Failure(TenantResolutionFailureReason.ResolutionNotAvailable));
		}

		Guid? userId = Guid.TryParse(request.Headers["X-User-Id"].FirstOrDefault(), out var parsedUserId) && parsedUserId != Guid.Empty
			? parsedUserId
			: null;
		return Task.FromResult(TenantResolutionResult.Success(tenantId, userId, TenantResolutionSource.DevelopmentHeader));
	}
}

public sealed class AuthenticatedTenantResolver(
	IHttpContextAccessor accessor,
	ITenantMembershipReader membershipReader) : ITenantResolver
{
	public async Task<TenantResolutionResult> ResolveAsync(CancellationToken cancellationToken)
	{
		var principal = accessor.HttpContext?.User;
		if (principal?.Identity?.IsAuthenticated != true)
		{
			return TenantResolutionResult.Failure(TenantResolutionFailureReason.NotAuthenticated);
		}

		var tenantClaims = principal.FindAll("tenant_id").Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray();
		if (tenantClaims.Length == 0)
		{
			return TenantResolutionResult.Failure(TenantResolutionFailureReason.NoTenantClaim);
		}

		if (tenantClaims.Length > 1)
		{
			return TenantResolutionResult.Failure(TenantResolutionFailureReason.AmbiguousTenant);
		}

		if (!Guid.TryParse(tenantClaims[0], out var tenantId) || tenantId == Guid.Empty)
		{
			return TenantResolutionResult.Failure(TenantResolutionFailureReason.TenantNotFound);
		}

		Guid? userId =
			Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedUserId)
			&& parsedUserId != Guid.Empty ? parsedUserId : null;

		if (userId is null)
		{
			return TenantResolutionResult.Failure(TenantResolutionFailureReason.UserNotMember);
		}

		var membership = await membershipReader.ReadMembershipAsync(tenantId, userId.Value, cancellationToken);
		if (!membership.IsSuccess)
		{
			return TenantResolutionResult.Failure(
				membership.FailureReason ?? TenantResolutionFailureReason.UserNotMember);
		}

		return TenantResolutionResult.Success(
			tenantId, userId, TenantResolutionSource.AuthenticatedClaim);
	}
}

public sealed class UnavailableTenantMembershipReader : ITenantMembershipReader
{
	public Task<TenantMembershipResult> ReadMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
	{
		return Task.FromResult(TenantMembershipResult.Failure(
			TenantResolutionFailureReason.UserNotMember,
			"TenantManagement membership verification is not implemented; access is denied."));
	}
}
