# EF Core migrations

No migrations are checked in yet — this build environment doesn't have the .NET SDK,
so they need to be generated once someone has it locally (Roselyn/whoever owns the
schema in Phase 1 is the natural owner of this step).

```bash
dotnet tool install --global dotnet-ef   # if not already installed
dotnet ef migrations add InitialCreate --project src/BiteShare.Data --startup-project src/BiteShare.Api
dotnet ef database update --project src/BiteShare.Data --startup-project src/BiteShare.Api
```

You'll need a real connection string first — see the `dotnet user-secrets set
"ConnectionStrings:Default" ...` command in the main README. `BiteShareDbContext` now
extends `IdentityDbContext<ApplicationUser>`, so the generated migration will include
the standard Identity tables (`AspNetUsers`, `AspNetRoles`, etc.) alongside `Sessions`,
`Participants`, `MenuItems`, `CartItems`, `Orders`, and `Receipts`.

Whoever runs this first should commit the generated `Migrations/` folder under
`src/BiteShare.Data/` and flag it in the team channel — per `CONTRIBUTING.md`, schema
changes are exactly the kind of shared-contract change that should be flagged before
merging, not discovered later.
