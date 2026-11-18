using Xunit;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using FormPlatform.Application.Abstractions;
using FormPlatform.Modules.Forms.Application;
using FormPlatform.Modules.Forms.Contracts;
using FormPlatform.Modules.Forms.Domain;
using FormPlatform.Modules.Forms.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace FormPlatform.Api.IntegrationTests;
public sealed class ApiTests : IClassFixture<ApiFactory>
{
 private readonly HttpClient _client; public ApiTests(ApiFactory factory){_client=factory.CreateClient();}
 [Fact] public async Task Health_endpoint_works() { var ct=CancellationToken.None; Assert.Equal(HttpStatusCode.OK,(await _client.GetAsync("/health",ct)).StatusCode); }
 [Fact] public async Task Api_rejects_missing_tenant() { _client.DefaultRequestHeaders.Remove("X-Tenant-Id"); var response=await _client.GetAsync("/api/v1/forms",CancellationToken.None); Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode); }
 [Fact] public async Task Swagger_document_is_available() { var ct=CancellationToken.None; Assert.Equal(HttpStatusCode.OK,(await _client.GetAsync("/swagger/v1/swagger.json",ct)).StatusCode); }
 [Fact] public async Task Form_version_publish_and_tenant_isolation_work()
 {
  var ct=CancellationToken.None; var a=Guid.NewGuid(); var b=Guid.NewGuid(); _client.DefaultRequestHeaders.Add("X-Tenant-Id",a.ToString());
  var response=await _client.PostAsJsonAsync("/api/v1/forms",new CreateFormRequest("contact","Contact",null),ct); Assert.Equal(HttpStatusCode.Created,response.StatusCode);
  var form=await response.Content.ReadFromJsonAsync<FormResponse>(cancellationToken:ct); Assert.NotNull(form);
  Assert.Equal(HttpStatusCode.OK,(await _client.GetAsync($"/api/v1/forms/{form.Id}",ct)).StatusCode);
  _client.DefaultRequestHeaders.Remove("X-Tenant-Id"); _client.DefaultRequestHeaders.Add("X-Tenant-Id",b.ToString());
  Assert.Equal(HttpStatusCode.NotFound,(await _client.GetAsync($"/api/v1/forms/{form.Id}",ct)).StatusCode);
  Assert.Equal(HttpStatusCode.OK,(await _client.GetAsync("/api/v1/forms",ct)).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await _client.PostAsJsonAsync($"/api/v1/forms/{form.Id}/versions",new CreateVersionRequest(null),ct)).StatusCode);
  _client.DefaultRequestHeaders.Remove("X-Tenant-Id"); _client.DefaultRequestHeaders.Add("X-Tenant-Id",a.ToString());
  var created=await _client.PostAsJsonAsync($"/api/v1/forms/{form.Id}/versions",new CreateVersionRequest("{\"metadata\":{},\"fields\":[]}"),ct); Assert.Equal(HttpStatusCode.Created,created.StatusCode);
  var ver=await created.Content.ReadFromJsonAsync<VersionResponse>(cancellationToken:ct); Assert.NotNull(ver);
  Assert.Equal(HttpStatusCode.OK,(await _client.PostAsync($"/api/v1/forms/{form.Id}/versions/{ver.Id}/publish",null,ct)).StatusCode);
  _client.DefaultRequestHeaders.Remove("X-Tenant-Id"); _client.DefaultRequestHeaders.Add("X-Tenant-Id",b.ToString());
  Assert.Equal(HttpStatusCode.NotFound,(await _client.PostAsync($"/api/v1/forms/{form.Id}/versions/{ver.Id}/publish",null,ct)).StatusCode);
  _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
 }

 [Fact]
 public async Task Submissions_require_a_published_version_round_trip_data_and_respect_tenant_scope()
 {
  var ct=CancellationToken.None; var tenantA=Guid.NewGuid(); var tenantB=Guid.NewGuid();
  _client.DefaultRequestHeaders.Add("X-Tenant-Id",tenantA.ToString());
  var formResponse=await _client.PostAsJsonAsync("/api/v1/forms",new CreateFormRequest($"submission-{Guid.NewGuid():N}","Submission test",null),ct);
  Assert.Equal(HttpStatusCode.Created,formResponse.StatusCode);
  var form=await formResponse.Content.ReadFromJsonAsync<FormResponse>(cancellationToken:ct); Assert.NotNull(form);

  const string schema="{\"metadata\":{},\"fields\":[{\"id\":\"name\",\"type\":\"text\",\"name\":\"Name\",\"configuration\":{}}]}";
  var versionResponse=await _client.PostAsJsonAsync($"/api/v1/forms/{form.Id}/versions",new CreateVersionRequest(schema),ct);
  Assert.Equal(HttpStatusCode.Created,versionResponse.StatusCode);
  var version=await versionResponse.Content.ReadFromJsonAsync<VersionResponse>(cancellationToken:ct); Assert.NotNull(version);
  var submissionRoute=$"/api/v1/runtime/forms/{form.Id}/versions/{version.Id}/submissions";

  var draftSubmission=await _client.PostAsJsonAsync(submissionRoute,new { data=new { name="Draft attempt" } },ct);
  Assert.Equal(HttpStatusCode.Conflict,draftSubmission.StatusCode);

  Assert.Equal(HttpStatusCode.OK,(await _client.PostAsync($"/api/v1/forms/{form.Id}/versions/{version.Id}/publish",null,ct)).StatusCode);
  var created=await _client.PostAsJsonAsync(submissionRoute,new { data=new { name="Ada", answers=new { choice=2 } } },ct);
  Assert.Equal(HttpStatusCode.Created,created.StatusCode);
  var submission=await created.Content.ReadFromJsonAsync<SubmissionResponse>(cancellationToken:ct); Assert.NotNull(submission);
  Assert.Equal("{\"name\":\"Ada\",\"answers\":{\"choice\":2}}",submission.Data.GetRawText());

  var read=await _client.GetFromJsonAsync<SubmissionResponse>($"/api/v1/runtime/submissions/{submission.Id}",ct);
  Assert.NotNull(read);
  Assert.Equal(submission.Data.GetRawText(),read.Data.GetRawText());
  using var listed=await JsonDocument.ParseAsync(await _client.GetStreamAsync($"/api/v1/forms/{form.Id}/submissions",ct),cancellationToken:ct);
  Assert.Equal(1,listed.RootElement.GetArrayLength());

  _client.DefaultRequestHeaders.Remove("X-Tenant-Id"); _client.DefaultRequestHeaders.Add("X-Tenant-Id",tenantB.ToString());
  Assert.Equal(HttpStatusCode.NotFound,(await _client.GetAsync($"/api/v1/runtime/submissions/{submission.Id}",ct)).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await _client.GetAsync($"/api/v1/forms/{form.Id}/submissions",ct)).StatusCode);
 }
}
public sealed class ApiFactory : WebApplicationFactory<Program>
{
 protected override void ConfigureWebHost(IWebHostBuilder builder)=>builder.ConfigureServices(services=>{
 services.RemoveAll<FormDbContext>(); services.RemoveAll<DbContextOptions<FormDbContext>>(); services.RemoveAll<IFormsRepository>(); services.AddSingleton<IFormsRepository,MemoryFormsRepository>();
 services.RemoveAll<ISubmissionRepository>(); services.AddSingleton<ISubmissionRepository,MemorySubmissionRepository>();
 });
}

public sealed class MemorySubmissionRepository : ISubmissionRepository
{
 private readonly ConcurrentDictionary<Guid,Submission> _submissions=[];
 public void Add(Submission submission)=>_submissions[submission.Id]=submission;
 public Task<Submission?> GetByIdAsync(Guid id,Guid tenant,CancellationToken ct)=>Task.FromResult(_submissions.TryGetValue(id,out var item)&&item.TenantId==tenant?item:null);
 public Task<IReadOnlyList<Submission>> ListByFormAsync(Guid formId,Guid tenant,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Submission>>(_submissions.Values.Where(item=>item.FormId==formId&&item.TenantId==tenant).OrderBy(item=>item.CreatedAtUtc).ToList());
 public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
}
public sealed class MemoryFormsRepository : IFormsRepository
{
 private readonly ConcurrentDictionary<Guid,Form> _forms=[];
 public Task AddAsync(Form f,CancellationToken ct){_forms[f.Id]=f;return Task.CompletedTask;}
 public void AddVersion(FormVersion version) { }
 public Task<Form?> GetAsync(Guid id,Guid tenant,CancellationToken ct)=>Task.FromResult(_forms.TryGetValue(id,out var f)&&f.TenantId==tenant?f:null);
 public Task<IReadOnlyList<Form>> ListAsync(Guid tenant,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Form>>(_forms.Values.Where(f=>f.TenantId==tenant).ToList());
 public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
}

public sealed class ProductionTenantTests : IClassFixture<ProductionApiFactory>
{
    private readonly HttpClient _client;

    public ProductionTenantTests(ProductionApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Header_tenant_is_not_resolved_outside_development()
    {
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        var response = await _client.GetAsync("/api/v1/forms", CancellationToken.None);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class ProductionApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseEnvironment("Production")
        .ConfigureServices(services =>
        {
            services.RemoveAll<FormDbContext>();
            services.RemoveAll<DbContextOptions<FormDbContext>>();
            services.RemoveAll<IFormsRepository>();
            services.AddSingleton<IFormsRepository, MemoryFormsRepository>();
        });
}
