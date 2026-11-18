using System.Data.Common;
using FormPlatform.Application.Abstractions;
using FormPlatform.Application.Abstractions.Tenancy;
using FormPlatform.Modules.Forms.Application;
using FormPlatform.Modules.Forms.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FormPlatform.Modules.Forms.Infrastructure;

public sealed class FormDbContext(DbContextOptions<FormDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<Form> Forms => Set<Form>();
    public DbSet<FormVersion> Versions => Set<FormVersion>();
    public DbSet<Submission> Submissions => Set<Submission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FormDbContext).Assembly);
        modelBuilder.Entity<Form>().HasQueryFilter(form => tenant.IsResolved && form.TenantId == tenant.TenantId);
        modelBuilder.Entity<FormVersion>().HasQueryFilter(version => tenant.IsResolved && version.TenantId == tenant.TenantId);
        modelBuilder.Entity<Submission>().HasQueryFilter(submission => tenant.IsResolved && submission.TenantId == tenant.TenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateVersionChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateVersionChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateVersionChanges()
    {
        foreach (var entry in ChangeTracker.Entries<FormVersion>())
        {
            var currentStatus = entry.Property(version => version.Status).CurrentValue;
            var publishedAt = entry.Property(version => version.PublishedAtUtc).CurrentValue;
            if (!Enum.IsDefined(currentStatus) ||
                (currentStatus == FormVersionStatus.Draft && publishedAt is not null) ||
                (currentStatus != FormVersionStatus.Draft && publishedAt is null))
            {
                throw new InvalidOperationException("The version publication state is inconsistent.");
            }

            if (entry.State == EntityState.Added)
            {
                continue;
            }

            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            var originalStatus = entry.Property(version => version.Status).OriginalValue;
            var allowedRetirement = originalStatus == FormVersionStatus.Published && currentStatus == FormVersionStatus.Retired;
            var immutablePropertiesChanged = HasChanged(entry, version => version.FormId) ||
                HasChanged(entry, version => version.TenantId) ||
                HasChanged(entry, version => version.VersionNumber) ||
                HasChanged(entry, version => version.SchemaVersion) ||
                HasChanged(entry, version => version.CreatedAtUtc);

            if (immutablePropertiesChanged ||
                (originalStatus != FormVersionStatus.Draft && HasChanged(entry, version => version.Schema)) ||
                (originalStatus != FormVersionStatus.Draft && HasChanged(entry, version => version.PublishedAtUtc)) ||
                (originalStatus != FormVersionStatus.Draft && currentStatus != originalStatus && !allowedRetirement) ||
                (originalStatus == FormVersionStatus.Draft && currentStatus == FormVersionStatus.Retired))
            {
                throw new InvalidOperationException("Persisted versions can only change while draft, and published versions may only be retired.");
            }
        }
    }

    private static bool HasChanged<TValue>(EntityEntry<FormVersion> entry, System.Linq.Expressions.Expression<Func<FormVersion, TValue>> propertyExpression)
    {
        var property = entry.Property(propertyExpression);
        return !EqualityComparer<TValue>.Default.Equals(property.OriginalValue, property.CurrentValue);
    }
}

internal sealed class FormConfiguration : IEntityTypeConfiguration<Form>
{
    public void Configure(EntityTypeBuilder<Form> builder)
    {
        builder.ToTable("Forms");
        builder.HasKey(form => form.Id);
        builder.HasAlternateKey(form => new { form.TenantId, form.Id }).HasName("AK_Forms_TenantId_Id");
        builder.Property(form => form.Code).HasMaxLength(100).IsRequired();
        builder.Property(form => form.Name).HasMaxLength(200).IsRequired();
        builder.Property(form => form.Description).HasMaxLength(2000);
        builder.Property<byte[]>("RowVersion").IsRowVersion().IsRequired();
        builder.HasIndex(form => new { form.TenantId, form.Code }).IsUnique();
        builder.HasMany(form => form.Versions)
            .WithOne()
            .HasForeignKey(version => new { version.TenantId, version.FormId })
            .HasPrincipalKey(form => new { form.TenantId, form.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(form => form.Versions)
            .HasField("_versions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class VersionConfiguration : IEntityTypeConfiguration<FormVersion>
{
    public void Configure(EntityTypeBuilder<FormVersion> builder)
    {
        builder.ToTable("FormVersions", table => table.HasCheckConstraint(
            "CK_FormVersions_PublicationState",
            "([Status] = 0 AND [PublishedAtUtc] IS NULL) OR ([Status] IN (1, 2) AND [PublishedAtUtc] IS NOT NULL)"));
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Schema).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property<byte[]>("RowVersion").IsRowVersion().IsRequired();
        builder.HasIndex(version => new { version.FormId, version.VersionNumber }).IsUnique();
    }
}

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions", "submissions");
        builder.HasKey(submission => submission.Id);
        builder.Property(submission => submission.DataJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(submission => new { submission.TenantId, submission.FormId, submission.CreatedAtUtc });
        builder.HasOne<Form>()
            .WithMany()
            .HasForeignKey(submission => new { submission.TenantId, submission.FormId })
            .HasPrincipalKey(form => new { form.TenantId, form.Id })
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<FormVersion>()
            .WithMany()
            .HasForeignKey(submission => new { submission.TenantId, submission.FormId, submission.FormVersionId })
            .HasPrincipalKey(version => new { version.TenantId, version.FormId, version.Id })
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class EfFormsRepository(FormDbContext db, ITenantContext tenant) : IFormsRepository
{
    public Task AddAsync(Form form, CancellationToken cancellationToken)
    {
        EnsureTenantOwns(form.TenantId);
        return db.Forms.AddAsync(form, cancellationToken).AsTask();
    }

    public void AddVersion(FormVersion version)
    {
        EnsureTenantOwns(version.TenantId);
        if (!db.Forms.Local.Any(form => form.Id == version.FormId && form.TenantId == version.TenantId))
        {
            throw new InvalidOperationException("The version parent must be loaded for the resolved tenant.");
        }

        db.Versions.Add(version);
    }

    public Task<Form?> GetAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        if (!tenant.IsResolved || tenant.TenantId == Guid.Empty || tenantId != tenant.TenantId)
        {
            return Task.FromResult<Form?>(null);
        }

        return db.Forms
            .Include(form => form.Versions)
            .SingleOrDefaultAsync(form => form.Id == id && form.TenantId == tenant.TenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<Form>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!tenant.IsResolved || tenant.TenantId == Guid.Empty || tenantId != tenant.TenantId)
        {
            return [];
        }

        return await db.Forms
            .AsNoTracking()
            .Where(form => form.TenantId == tenant.TenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var existingParents = db.ChangeTracker.Entries<Form>()
            .Where(entry => entry.State != EntityState.Added)
            .Where(entry => db.ChangeTracker.Entries<FormVersion>().Any(version =>
                version.State == EntityState.Added && version.Entity.FormId == entry.Entity.Id))
            .ToList();

        try
        {
            if (existingParents.Count == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var parent in existingParents)
            {
                await TouchParentConcurrencyTokenAsync(parent, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConcurrencyException("A concurrent update prevented the operation.", exception);
        }
        catch (DbUpdateException exception) when (IsSqlServerUniqueConstraintConflict(exception))
        {
            throw new PersistenceConstraintException("A unique persistence constraint was violated.", exception);
        }
    }

    private async Task TouchParentConcurrencyTokenAsync(EntityEntry<Form> parent, CancellationToken cancellationToken)
    {
        var rowVersion = parent.Property<byte[]>("RowVersion");
        if (rowVersion.OriginalValue is not { Length: > 0 } expectedRowVersion)
        {
            throw new PersistenceConcurrencyException("The parent form has no concurrency token.");
        }

        var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            UPDATE [Forms]
            SET [UpdatedAtUtc] = @updatedAtUtc
            OUTPUT INSERTED.[RowVersion]
            WHERE [Id] = @id AND [TenantId] = @tenantId AND [RowVersion] = @rowVersion;
            """;
        AddParameter(command, "@updatedAtUtc", parent.Property(form => form.UpdatedAtUtc).CurrentValue);
        AddParameter(command, "@id", parent.Entity.Id);
        AddParameter(command, "@tenantId", parent.Entity.TenantId);
        AddParameter(command, "@rowVersion", expectedRowVersion);

        await using (command)
        {
            var refreshedRowVersion = await command.ExecuteScalarAsync(cancellationToken);
            if (refreshedRowVersion is not byte[] newRowVersion)
            {
                throw new PersistenceConcurrencyException("The parent form changed concurrently.");
            }

            rowVersion.CurrentValue = newRowVersion;
            rowVersion.OriginalValue = newRowVersion;
            rowVersion.IsModified = false;

            var updatedAt = parent.Property(form => form.UpdatedAtUtc);
            updatedAt.OriginalValue = updatedAt.CurrentValue;
            updatedAt.IsModified = false;
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private void EnsureTenantOwns(Guid recordTenantId)
    {
        if (!tenant.IsResolved || tenant.TenantId == Guid.Empty || recordTenantId != tenant.TenantId)
        {
            throw new InvalidOperationException("The record does not belong to the resolved tenant.");
        }
    }

    private static bool IsSqlServerUniqueConstraintConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

public sealed class EfSubmissionRepository(FormDbContext db) : ISubmissionRepository
{
    public void Add(Submission submission) => db.Submissions.Add(submission);

    public Task<Submission?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        db.Submissions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<Submission>> ListByFormAsync(Guid formId, Guid tenantId, CancellationToken cancellationToken) =>
        await db.Submissions.AsNoTracking()
            .Where(item => item.FormId == formId && item.TenantId == tenantId)
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await db.SaveChangesAsync(cancellationToken);
}

public static class FormsInfrastructureRegistration
{
    public static IServiceCollection AddFormsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Forms") ??
            "Server=(localdb)\\MSSQLLocalDB;Database=FormPlatform;Trusted_Connection=True;TrustServerCertificate=True";
        services.AddDbContext<FormDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IFormsRepository, EfFormsRepository>();
        services.AddScoped<ISubmissionRepository, EfSubmissionRepository>();
        return services;
    }
}
