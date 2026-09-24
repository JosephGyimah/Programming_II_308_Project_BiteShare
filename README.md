# BiteShare

BiteShare is a collaborative group-ordering app: a host opens a session, participants join and build a shared cart in real time, the host submits the order, payment is captured per participant, and everyone gets live status updates through to delivery.

## Tech stack

| Layer | Tech |
|---|---|
| API | ASP.NET Core Web API (minimal API / controllers) |
| Client | Blazor WebAssembly |
| Shared | Common DTOs/models used by API and Client |
| Data | EF Core + PostgreSQL (Npgsql) |
| Real-time | SignalR (`OrderHub`) |
| Auth | ASP.NET Core Identity + JWT, plus anonymous guest-participant tokens |
| Payments | Stripe .NET SDK |
| Hosting | Render (Docker web service + Postgres), auto-deployed from `main` |

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

The API applies EF Core migrations automatically on startup, so there is no separate
database step. To apply them by hand instead:

```bash
dotnet ef database update --project src/BiteShare.Data --startup-project src/BiteShare.Api
```

Run tests:

```bash
dotnet test
```

## Configuration

The API reads connection strings and secrets from `appsettings.json` for local defaults and user-secrets (locally) or environment variables (on Render) for anything sensitive (database connection string, JWT signing key, Stripe keys). Never commit real secrets — see `.gitignore`.

```bash
dotnet user-secrets init --project src/BiteShare.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=biteshare;Username=<user>;Password=<password>" --project src/BiteShare.Api
dotnet user-secrets set "Jwt:SigningKey" "<a-long-random-string-at-least-32-chars>" --project src/BiteShare.Api
dotnet user-secrets set "Stripe:SecretKey" "<your-test-key>" --project src/BiteShare.Api
```

Locally you need a running PostgreSQL database (create an empty one named `biteshare`) and
`Jwt:SigningKey` set: token issuing fails without it, and every team member's local API
should use the *same* key. Stripe keys can stay blank — the Session page skips payment.

On a host that provides `DATABASE_URL` (postgres://...), such as Render, that variable is
used instead of `ConnectionStrings:Default`.

## Further docs

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — system diagram, the two-JWT-type model, SignalR reconnect behavior, cost-splitting rules
- [`docs/API.md`](docs/API.md) — endpoint reference
- [`docs/MIGRATIONS.md`](docs/MIGRATIONS.md) — how EF Core migrations work in this repo (Postgres, applied on startup)

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

GitHub Actions (`.github/workflows/ci.yml`) builds and tests on every push and pull request to `main`.

### Deployment (Render)

Render builds the `Dockerfile` (API + Blazor client in one container) and provisions a Postgres
database from `render.yaml`, and auto-deploys on every push to `main`.

1. Push the repo to GitHub.
2. Render dashboard → **New → Blueprint** → select the repo. It reads `render.yaml`.
3. Wait for the first build, then open the `https://biteshare-….onrender.com` URL and sign up.

`Jwt__SigningKey` is generated for you and `DATABASE_URL` is wired to the database. The API
creates the tables itself on first start.

**Every ~30 days** the free Postgres expires. We accept this and recreate it (all accounts,
sessions and orders are wiped — the app is a demo, so that's fine):

1. Render dashboard → `biteshare-db` → delete it (or let it expire).
2. Dashboard → **Blueprints** → the BiteShare blueprint → **Manual sync**. This recreates the
   database and re-links `DATABASE_URL` to the web service.
3. If the site doesn't recover on its own, open the `biteshare` web service → **Manual Deploy →
   Deploy latest commit**. On start the API recreates the tables.
4. Sign up again — old accounts are gone.
