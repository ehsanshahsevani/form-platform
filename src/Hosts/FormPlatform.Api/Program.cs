using FormPlatform.Api;
using FormPlatform.Api.Endpoints;
using FormPlatform.Modules.Forms.Application;
using FormPlatform.Modules.Forms.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication();
builder.Services.AddTenancy(builder.Environment);
builder.Services.AddFormsApplication();
builder.Services.AddFormsInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthentication();
app.UseTenantResolution();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapFormsEndpoints();
app.MapRuntimeEndpoints();

app.Run();

public partial class Program;
