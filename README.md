# BiteShare

BiteShare is a collaborative group-ordering app: a host opens a session, participants join and build a shared cart in real time, the host submits the order, payment is captured per participant, and everyone gets live status updates through to delivery.

## Tech stack

| Layer | Tech |
|---|---|
| API | ASP.NET Core Web API (minimal API / controllers) |
| Client | Blazor WebAssembly |
| Shared | Common DTOs/models used by API and Client |
| Data | EF Core + Azure SQL |
| Real-time | SignalR (`OrderHub`) |
| Auth | ASP.NET Core Identity + JWT, plus anonymous guest-participant tokens |
| Payments | Stripe .NET SDK |
| Hosting | Azure App Service, deployed via GitHub Actions |

## Solution layout

```
BiteShare.sln
src/
  BiteShare.Api/       ASP.NET Core Web API, controllers, SignalR hub
  BiteShare.Client/    Blazor WebAssembly front end
  BiteShare.Shared/    DTOs and models shared by Api and Client
  BiteShare.Data/      EF Core DbContext + migrations
tests/
  BiteShare.Tests/     Unit + integration tests
```

## Getting started

```bash
git clone <repo-url>
cd BiteShare
dotnet restore
dotnet build
```

Run the API:

```bash
dotnet run --project src/BiteShare.Api
```

Run the Blazor client (in a second terminal):

```bash
dotnet run --project src/BiteShare.Client
```

Apply EF Core migrations (once the schema is in place):

```bash
dotnet ef database update --project src/BiteShare.Data --startup-project src/BiteShare.Api
```

Run tests:

```bash
dotnet test
```

## Configuration

The API reads connection strings and secrets from `appsettings.json` for local defaults and user-secrets / Azure App Configuration for anything sensitive (SQL connection string, JWT signing key, Stripe keys). Never commit real secrets — see `.gitignore`.

```bash
dotnet user-secrets init --project src/BiteShare.Api
dotnet user-secrets set "ConnectionStrings:Default" "<your-local-connection-string>" --project src/BiteShare.Api
dotnet user-secrets set "Jwt:SigningKey" "<a-long-random-string-at-least-32-chars>" --project src/BiteShare.Api
dotnet user-secrets set "Stripe:SecretKey" "<your-test-key>" --project src/BiteShare.Api
```

The API will still start without `Jwt:SigningKey` set (it falls back to an obviously-fake
dev placeholder so `dotnet run` doesn't crash on a fresh clone), but auth will only work
correctly once every team member's local API uses the *same* signing key — set it via
user-secrets, don't rely on the fallback past your very first run.

## Further docs

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — system diagram, the two-JWT-type model, SignalR reconnect behavior, cost-splitting rules
- [`docs/API.md`](docs/API.md) — endpoint reference
- [`docs/MIGRATIONS.md`](docs/MIGRATIONS.md) — generating the initial EF Core migration (not checked in yet — needs the .NET SDK)

## Team & roles

See `CONTRIBUTING.md` for coding standards, branching, and PR process, `CODEOWNERS` for per-area ownership, and the project execution guide for the full phase-by-phase plan.

| Name | Role |
|---|---|
| Precious Ayomah | **Project Lead** |
| Somuah Kofi Anim | Default owner / build & bootstrap |
| Stephanie Apenteng | Process, docs & README owner |
| Priscilla Akuokor | CI/CD & deployment |
| Roselyn Sakyi | Data layer, cost splitter & receipts |
| Horoya Razak | Auth & identity |
| Aaron Tetteh | Real-time / collaborative cart (Stream A) |
| Joseph Gyimah | Client services |
| Olivia Kwateng | Orders & payments (Stream C) |
| Obadiah Donkor | Sessions, catalog & tests |
| Emmanuel Grant Boamah | Config & participants |

## Core domain model

`Session → Participant → CartItem → Order → Receipt`, with `MenuItem` as the catalog a session's cart draws from. Schema lives in `BiteShare.Data`.

## CI/CD

GitHub Actions (`.github/workflows/azure-deploy.yml`) builds, tests, and deploys to Azure App Service on merge to `main`.
