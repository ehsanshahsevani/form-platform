# Project Structure

- `FormPlatform.Domain.Abstractions`: entities, aggregate roots, value objects, and domain event primitives.
- `FormPlatform.Application.Abstractions`: result and error types, current-user, clock, and tenant context contracts.
- `FormPlatform.Modules.Forms.Domain`: Forms and FormVersion rules plus independent JSON schema validation.
- `FormPlatform.Modules.Forms.Application`: tenant-aware use cases, repository contract, expected error results, and module registration.
- `FormPlatform.Modules.Forms.Contracts`: HTTP request and response DTOs.
- `FormPlatform.Modules.Forms.Infrastructure`: SQL Server EF Core context, tenant query filters, entity mappings, repository, concurrency translation, and migrations.
- `FormPlatform.Api`: module composition, development-only tenant-header resolution, request tenant guard, error middleware, Swagger, health, and Forms endpoints.
- `FormPlatform.Modules.Forms.UnitTests`: domain transitions, schema validation, application tenant checks, and Result invariants.
- `FormPlatform.Api.IntegrationTests`: API behavior plus optional real SQL Server persistence tests.

Build and run the unit/API suite with `dotnet build FormPlatform.slnx` and `dotnet test FormPlatform.slnx`. Set `FORMPLATFORM_SQLSERVER` to a dedicated SQL Server connection string to include relational persistence tests. Those tests create a uniquely named database, apply all migrations, check SQL constraints and concurrency, then delete only that generated test database. Configure `ConnectionStrings:Forms` for the API and run migrations with the command in the README.
