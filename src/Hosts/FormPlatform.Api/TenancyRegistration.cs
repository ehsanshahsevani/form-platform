using FormPlatform.Application.Abstractions.Tenancy;
using FormPlatform.Infrastructure.Tenancy;
using FormPlatform.Infrastructure.Tenancy.Development;

namespace FormPlatform.Api;

public static class TenancyRegistration
{
    public static IServiceCollection AddTenancy(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ScopedTenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<ScopedTenantContext>());

        if (environment.IsDevelopment())
        {
            services.AddScoped<ITenantMembershipReader, DevelopmentStubTenantMembershipReader>();
            services.AddScoped<ITenantResolver, DevelopmentHeaderTenantResolver>();
        }
        else
        {
            services.AddScoped<ITenantMembershipReader, UnavailableTenantMembershipReader>();
            services.AddScoped<ITenantResolver, AuthenticatedTenantResolver>();
        }

        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}

internal sealed class ScopedTenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public bool IsResolved { get; private set; }
    public TenantResolutionSource Source { get; private set; }

    public void Set(TenantResolutionResult result)
    {
        TenantId = result.IsSuccess ? result.TenantId!.Value : Guid.Empty;
        UserId = result.IsSuccess ? result.UserId : null;
        IsResolved = result.IsSuccess;
        Source = result.IsSuccess ? result.Source : TenantResolutionSource.None;
    }
}

internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver, ScopedTenantContext tenantContext)
    {
        tenantContext.Set(await resolver.ResolveAsync(context.RequestAborted));
        await next(context);
    }
}
