using FormPlatform.Application.Abstractions;
using FormPlatform.Application.Abstractions.Tenancy;
using FormPlatform.Modules.Forms.Application;
using FormPlatform.Modules.Forms.Domain;
using FormPlatform.Modules.Forms.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FormPlatform.Api.IntegrationTests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(SqlServerTestConnection.GetConnectionString()))
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Skip = "Set FORM_PLATFORM_SQL_CONNECTION or FORMPLATFORM_SQLSERVER to a dedicated SQL Server connection string to run relational persistence tests.";
            }
        }
    }
}

internal static class SqlServerTestConnection
{
    public static string? GetConnectionString() =>
        Environment.GetEnvironmentVariable("FORM_PLATFORM_SQL_CONNECTION") ??
        Environment.GetEnvironmentVariable("FORMPLATFORM_SQLSERVER");
}

public sealed class SqlServerPersistenceTests
{
    private const string ValidSchema = "{\"metadata\":{},\"fields\":[{\"id\":\"name\",\"type\":\"text\",\"name\":\"Name\",\"configuration\":{}}]}";

    [SqlServerFact]
    public async Task Sql_server_migrations_constraints_filters_and_concurrency_work()
    {
        var serverConnection = SqlServerTestConnection.GetConnectionString();
        Assert.True(!string.IsNullOrWhiteSpace(serverConnection),
            "FORM_PLATFORM_SQL_CONNECTION or FORMPLATFORM_SQLSERVER is required in CI; configure the SQL Server service connection string.");
        var databaseName = $"FormPlatformHardening_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(serverConnection!)
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true
        }.ConnectionString;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        try
        {
            await using (var context = CreateContext(connection, tenantA))
            {
                await context.Database.MigrateAsync();
                var migrations = await context.Database.GetAppliedMigrationsAsync();
                Assert.Contains(migrations, migration => migration.EndsWith("InitialForms", StringComparison.Ordinal));
                Assert.Contains(migrations, migration => migration.EndsWith("TenantIntegrityAndConcurrency", StringComparison.Ordinal));

                var formModel = context.Model.FindEntityType(typeof(Form));
                var versionModel = context.Model.FindEntityType(typeof(FormVersion));
                Assert.NotNull(formModel);
                Assert.NotNull(versionModel);
                Assert.Contains(formModel.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "Code"]));
                Assert.Contains(versionModel.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["FormId", "VersionNumber"]));
                var relationship = Assert.Single(versionModel.GetForeignKeys());
                Assert.Equal(new[] { "TenantId", "FormId" }, relationship.Properties.Select(property => property.Name));
                Assert.Equal(new[] { "TenantId", "Id" }, relationship.PrincipalKey.Properties.Select(property => property.Name));

                var form = Form.Create(tenantA, "shared-code", "Tenant A", null, FormStatus.Draft, now);
                var version = form.CreateDraftVersion(now);
                version.UpdateSchema(ValidSchema);
                version.Publish(now);
                context.Forms.Add(form);
                await context.SaveChangesAsync();

                context.Submissions.Add(Submission.Create(tenantA, form.Id, version.Id, "{\"name\":\"Ada\"}", now.AddMinutes(1)));
                await context.SaveChangesAsync();
            }

            await using (var tenantAContext = CreateContext(connection, tenantA))
            {
                var form = await tenantAContext.Forms.Include(item => item.Versions).SingleAsync();
                Assert.Equal(tenantA, form.TenantId);
                Assert.Single(form.Versions);
                var submission = await tenantAContext.Submissions.SingleAsync();
                Assert.Equal(form.Id, submission.FormId);
                Assert.Equal("{\"name\":\"Ada\"}", submission.DataJson);

                var publishedVersion = form.Versions.Single();
                tenantAContext.Entry(publishedVersion).Property(item => item.Schema).CurrentValue = "{\"metadata\":{},\"fields\":[]}";
                await Assert.ThrowsAsync<InvalidOperationException>(() => tenantAContext.SaveChangesAsync());
            }

            await using (var tenantBContext = CreateContext(connection, tenantB))
            {
                Assert.Empty(await tenantBContext.Forms.ToListAsync());
                Assert.Empty(await tenantBContext.Versions.ToListAsync());
                Assert.Empty(await tenantBContext.Submissions.ToListAsync());
            }

            await using (var retirementContext = CreateContext(connection, tenantA))
            {
                var published = await retirementContext.Versions.SingleAsync();
                published.Retire();
                await retirementContext.SaveChangesAsync();
            }

            await using (var retiredContext = CreateContext(connection, tenantA))
            {
                var retired = await retiredContext.Versions.SingleAsync();
                retiredContext.Entry(retired).Property(version => version.Schema).CurrentValue = "{\"metadata\":{},\"fields\":[]}";
                await Assert.ThrowsAsync<InvalidOperationException>(() => retiredContext.SaveChangesAsync());
            }

            await using (var repositoryContext = CreateContext(connection, tenantA))
            {
                var repository = new EfFormsRepository(repositoryContext, new TestTenantContext(tenantA));
                Assert.Null(await repository.GetAsync(Guid.NewGuid(), tenantB, CancellationToken.None));
                Assert.Empty(await repository.ListAsync(tenantB, CancellationToken.None));
                Assert.Throws<InvalidOperationException>(() =>
                {
                    _ = repository.AddAsync(
                        Form.Create(tenantB, "wrong-tenant", "Wrong tenant", null, FormStatus.Draft, now),
                        CancellationToken.None);
                });
            }

            await AssertDatabaseConstraintViolation(connection, tenantA, async context =>
            {
                context.Forms.Add(Form.Create(tenantA, "shared-code", "Duplicate code", null, FormStatus.Draft, now.AddSeconds(1)));
                await context.SaveChangesAsync();
            });

            await using (var duplicateVersionContext = CreateContext(connection, tenantA))
            {
                var formId = (await duplicateVersionContext.Forms.SingleAsync()).Id;
                await Assert.ThrowsAsync<SqlException>(() => duplicateVersionContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [FormVersions] ([Id], [FormId], [TenantId], [VersionNumber], [SchemaVersion], [Status], [Schema], [CreatedAtUtc], [PublishedAtUtc])
                    VALUES ({Guid.NewGuid()}, {formId}, {tenantA}, {1}, {1}, {0}, {"{\"metadata\":{},\"fields\":[]}"}, {now}, {null})
                    """));
            }

            await AssertDatabaseConstraintViolation(connection, tenantA, async context =>
            {
                var otherTenantForm = Form.Create(tenantB, "other", "Tenant B", null, FormStatus.Draft, now.AddSeconds(3));
                context.Forms.Add(otherTenantForm);
                await context.SaveChangesAsync();
                var crossTenantVersion = otherTenantForm.CreateDraftVersion(now.AddSeconds(3));
                context.Versions.Add(crossTenantVersion);
                context.Entry(crossTenantVersion).Property(item => item.TenantId).CurrentValue = tenantA;
                await context.SaveChangesAsync();
            });

            await AssertConcurrentDraftCreationConflicts(connection, tenantA, now);
        }
        finally
        {
            await using var cleanup = CreateContext(connection, tenantA);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static async Task AssertDatabaseConstraintViolation(string connection, Guid tenantId, Func<FormDbContext, Task> operation)
    {
        await using var context = CreateContext(connection, tenantId);
        await Assert.ThrowsAsync<DbUpdateException>(() => operation(context));
    }

    private static async Task AssertConcurrentDraftCreationConflicts(string connection, Guid tenantId, DateTime now)
    {
        Guid formId;
        await using (var seed = CreateContext(connection, tenantId))
        {
            var form = Form.Create(tenantId, "concurrent", "Concurrent", null, FormStatus.Draft, now);
            seed.Forms.Add(form);
            await seed.SaveChangesAsync();
            formId = form.Id;
        }

        await using var firstContext = CreateContext(connection, tenantId);
        await using var secondContext = CreateContext(connection, tenantId);
        var firstRepository = new EfFormsRepository(firstContext, new TestTenantContext(tenantId));
        var secondRepository = new EfFormsRepository(secondContext, new TestTenantContext(tenantId));
        var firstForm = await firstRepository.GetAsync(formId, tenantId, CancellationToken.None);
        var secondForm = await secondRepository.GetAsync(formId, tenantId, CancellationToken.None);
        Assert.NotNull(firstForm);
        Assert.NotNull(secondForm);
        var firstVersion = firstForm.CreateDraftVersion(now.AddMinutes(1));
        var secondVersion = secondForm.CreateDraftVersion(now.AddMinutes(2));
        firstRepository.AddVersion(firstVersion);
        secondRepository.AddVersion(secondVersion);
        Assert.Equal(EntityState.Added, firstContext.Entry(firstVersion).State);
        try
        {
            await firstRepository.SaveChangesAsync(CancellationToken.None);
        }
        catch (PersistenceConcurrencyException exception) when (exception.InnerException is DbUpdateConcurrencyException concurrencyException)
        {
            var entities = string.Join(", ", concurrencyException.Entries.Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}"));
            throw new Xunit.Sdk.XunitException($"The first save unexpectedly conflicted for: {entities}.");
        }

        await Assert.ThrowsAsync<PersistenceConcurrencyException>(() => secondRepository.SaveChangesAsync(CancellationToken.None));
    }

    private static FormDbContext CreateContext(string connection, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FormDbContext>().UseSqlServer(connection).Options;
        return new FormDbContext(options, new TestTenantContext(tenantId));
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid? UserId => Guid.NewGuid();
        public bool IsResolved => TenantId != Guid.Empty;
        public TenantResolutionSource Source => IsResolved ? TenantResolutionSource.AuthenticatedClaim : TenantResolutionSource.None;
    }
}
