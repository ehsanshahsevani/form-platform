using System.Security.Claims;
using FormPlatform.Application.Abstractions.Tenancy;
using FormPlatform.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FormPlatform.Api.IntegrationTests;

public sealed class TenantResolverTests
{
    [Fact]
    public async Task Authenticated_claim_without_membership_is_denied()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "test-authentication"));

        var resolver = new AuthenticatedTenantResolver(
            new HttpContextAccessor { HttpContext = context },
            new UnavailableTenantMembershipReader());

        var result = await resolver.ResolveAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TenantResolutionFailureReason.UserNotMember, result.FailureReason);
    }

    [Fact]
    public async Task Development_header_resolver_still_accepts_development_header()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        context.Request.Headers["X-User-Id"] = userId.ToString();

        var resolver = new DevelopmentHeaderTenantResolver(new HttpContextAccessor { HttpContext = context });

        var result = await resolver.ResolveAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(TenantResolutionSource.DevelopmentHeader, result.Source);
    }
}
