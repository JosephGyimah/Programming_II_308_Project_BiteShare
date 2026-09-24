# EF Core migrations

The schema targets **PostgreSQL** (Npgsql). Migrations live in `src/BiteShare.Data/Migrations/`
and are checked in. The API applies any pending migrations automatically on startup
(`db.Database.Migrate()` in `BiteShare.Api/Program.cs`), so a fresh database — locally or on
Render — needs no manual step. Startup skips this for non-relational providers, which is what
lets the integration tests run on the in-memory database.

`BiteShareDbContext` extends `IdentityDbContext<ApplicationUser>`, so the schema includes the
standard Identity tables (`AspNetUsers`, `AspNetRoles`, ...) alongside `Sessions`,
`Participants`, `MenuItems`, `CartItems`, `Orders`, and `Receipts`.

## Changing the schema

After editing entities or `BiteShareDbContext`, generate a new migration and commit it:

```bash
dotnet tool install --global dotnet-ef   # if not already installed
dotnet ef migrations add <DescriptiveName> --project src/BiteShare.Data --startup-project src/BiteShare.Api
```

Applying it is automatic on the next start; to apply by hand:

```bash
dotnet ef database update --project src/BiteShare.Data --startup-project src/BiteShare.Api
```

You need a connection string first — see the Configuration section of the main README.
Per `CONTRIBUTING.md`, schema changes are a shared-contract change: flag them in the team
channel before merging.

## History

The original `InitialCreate` targeted SQL Server / Azure SQL. It was replaced with a Postgres
`InitialCreate` when hosting moved to Render (which has no SQL Server). Any database created
from the old migration must be recreated — the two are not compatible.
