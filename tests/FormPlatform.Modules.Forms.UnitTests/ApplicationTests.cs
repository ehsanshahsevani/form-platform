using FormPlatform.Application.Abstractions;
using FormPlatform.Application.Abstractions.Tenancy;
using FormPlatform.Modules.Forms.Application;
using FormPlatform.Modules.Forms.Domain;
using Xunit;

namespace FormPlatform.Modules.Forms.UnitTests;

public sealed class ApplicationTests
{
    [Fact]
    public async Task Application_rejects_unresolved_tenant_before_repository_access()
    {
        var repository = new InMemoryRepository();
        var service = new FormsService(repository, new TestTenantContext(), new SystemClock());

        var result = await service.CreateAsync(new("code", "name", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Tenant.Required", result.Error?.Code);
        Assert.Empty(repository.Forms);
    }

    [Fact]
    public void Generic_result_rejects_null_success_values()
    {
        string? noValue = null;
        Assert.Throws<ArgumentNullException>(() => Result<string?>.Success(noValue));
    }

    [Fact]
    public void Failed_generic_result_cannot_be_read_as_a_success_value()
    {
        var result = Result<int>.Failure(new Error("Expected", "Expected failure."));
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Result_rejects_empty_errors()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(new Error(string.Empty, "Missing code.")));
        Assert.Throws<ArgumentException>(() => Result<string>.Failure(new Error("MissingDescription", " ")));
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid? UserId => null;
        public bool IsResolved => false;
        public TenantResolutionSource Source => TenantResolutionSource.None;
    }

    private sealed class InMemoryRepository : IFormsRepository
    {
        public List<Form> Forms { get; } = [];
        public void AddVersion(FormVersion version) { }
        public Task AddAsync(Form form, CancellationToken cancellationToken)
        {
            Forms.Add(form);
            return Task.CompletedTask;
        }
        public Task<Form?> GetAsync(Guid id, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(Forms.SingleOrDefault(form => form.Id == id && form.TenantId == tenantId));
        public Task<IReadOnlyList<Form>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Form>>(Forms.Where(form => form.TenantId == tenantId).ToList());
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
