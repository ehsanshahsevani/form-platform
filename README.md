# FormPlatform

A .NET 10 modular monolith foundation for tenant-owned forms and schema versions.

## Run the API

Set `ConnectionStrings:Forms` to a reachable SQL Server connection, apply migrations, and start the host:

```sh
dotnet ef database update --project src/Modules/Forms/FormPlatform.Modules.Forms.Infrastructure --startup-project src/Hosts/FormPlatform.Api
dotnet run --project src/Hosts/FormPlatform.Api
```

Swagger UI is at `/swagger`; health is at `/health`. In Development, Forms requests require `X-Tenant-Id` and may include `X-User-Id`. The header resolver is not registered in other environments. Production Forms requests remain rejected until authenticated tenant resolution is connected. Never send tenant identity in request bodies.

## Build and test

```sh
dotnet build FormPlatform.slnx
dotnet test FormPlatform.slnx
```

The unit and HTTP tests run without SQL Server. To run the relational persistence test against a dedicated SQL Server instance, set `FORMPLATFORM_SQLSERVER` to its connection string before `dotnet test`. The test creates a uniquely named database, applies the migrations, verifies relational constraints and concurrency, and drops that generated database afterward.

See [Architecture](docs/Architecture.md) and [Project Structure](docs/ProjectStructure.md).
# form-platform
