using FormPlatform.Application.Abstractions.Tenancy;

namespace FormPlatform.Api;

internal static class TenantEndpointExtensions
{
    public static RouteGroupBuilder MapTenantGroup(this IEndpointRouteBuilder endpoints, string prefix)
    {
        var group = endpoints.MapGroup(prefix);
        group.AddEndpointFilter(async (context, next) =>
        {
            var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
            if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Tenant context required");
            }

            return await next(context);
        });

        return group;
    }
}
