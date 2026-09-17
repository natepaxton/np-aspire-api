# np-aspire-api

Backend for np-aspire: a single .NET 10 API orchestrated by Aspire (the single source of truth for topology — no hand-written Docker/compose files, no gateway yet). The Angular frontend is a separate repo: https://github.com/natepaxton/np-aspire

Full spec, architecture, and open decisions: @docs/spec.md

## Stack

- **API:** .NET 10 (LTS) ASP.NET Core (controllers), PostgreSQL, EF Core + Dapper (Npgsql), OpenAPI (the contract np-aspire generates its client from)
- **Auth:** Auth0 — the API validates JWT access tokens; login/tokens are handled by Auth0 and the SPA
- **Orchestration:** Aspire 13 AppHost — the only place topology is defined; deployment files come from the Aspire Docker Compose publisher (`aspire publish` → `src/NpAspire.AppHost/aspire-output/`, gitignored)
- **Auth0 config:** Terraform (`infra/auth0/`, `auth0/auth0` provider), driven by `infra/auth0/permissions.json`
- **Tests:** xUnit v3 on Microsoft Testing Platform, `Microsoft.AspNetCore.Mvc.Testing`, `coverlet.MTP` + ReportGenerator
- **CI:** GitHub Actions (`.github/workflows/ci.yml`)
- **Deferred:** NGINX gateway (until there are multiple APIs), deployment pipeline and production host, microservices split, RabbitMQ, BFF auth (see spec §7, §8)

## Layout

- `src/NpAspire.Api` the API; `src/NpAspire.AppHost` Aspire AppHost; `src/NpAspire.ServiceDefaults` shared Aspire defaults
- `tests/NpAspire.Api.Tests` API tests
- `infra/auth0/` Terraform + permissions manifest (`.env.local` there is gitignored and holds the Terraform credentials)
- `scripts/` repo scripts (`coverage-check.mjs`)

## Commands

- After cloning: `dotnet tool restore` (ReportGenerator, pinned in `dotnet-tools.json`)
- Auth0 settings for local runs (AppHost parameters, not secrets): `dotnet user-secrets set "Parameters:auth0-domain" "<tenant>.us.auth0.com" --project src/NpAspire.AppHost` and the same for `Parameters:auth0-audience` (the Auth0 API identifier); the dashboard prompts if they're missing
- Test auth manually: `curl -H "Authorization: Bearer <token>" http://localhost:5104/api/v1/auth/check` (token from Auth0 dashboard → APIs → Test); Rider reads `accessToken` from gitignored `src/NpAspire.Api/http-client.private.env.json`
- Build: `dotnet build np-aspire-api.slnx`
- Test: `dotnet test --solution np-aspire-api.slnx`
- Test with coverage + minimums (as CI does): `dotnet build np-aspire-api.slnx && node scripts/coverage-check.mjs --project tests/NpAspire.Api.Tests --out coverage --lines 80 --branches 75 --methods 80`
- Format: `dotnet format np-aspire-api.slnx` (CI runs `--verify-no-changes`)
- Run everything (dev): `dotnet run --project src/NpAspire.AppHost` (the AppHost SDK bundles what it needs)
- Aspire CLI (optional, machine-wide — not in the tool manifest, see spec §3.3): `curl -sSL https://aspire.dev/install.sh | bash`; then `aspire run`, `aspire describe`
- Generate Docker Compose files (needs the Aspire CLI): `aspire publish` (output in `src/NpAspire.AppHost/aspire-output/`)
- Auth0 (dev tenant): `set -a; source infra/auth0/.env.local; set +a`, then `terraform -chdir=infra/auth0 plan -var-file=env/dev.tfvars` / `apply` (spec §5.2)

## Rules

- API routes are `api/v{version}/<plural-resource>` (`Asp.Versioning.Mvc` from milestone 3); controllers are plural (`UsersController`). The prefix is applied globally by `Routing/RoutePrefixConvention` (currently `api/v1`), so controllers declare only the resource: `[Route("users")]`, never `[Route("api/v1/users")]`. Keep the `/api/v1/` prefix so a gateway can be added later without client changes.
- EF Core for writes and migrations, Dapper for read-heavy queries.
- The API validates Auth0 JWTs itself (`AddAuth0Authentication`: RS256 only, `MapInboundClaims = false`, config section `Auth0` validated on start). Auth0 owns login/tokens — don't build login/logout/token endpoints. `AuthController` is diagnostics only (`GET /api/v1/auth/check`); `DiagnosticsController` is the public status check (`GET /api/v1/diagnostics`, runs all health checks, returns only the overall status).
- API is deny-by-default (fallback policy requires auth); opt out explicitly with `[AllowAnonymous]`/`.AllowAnonymous()`. Anonymous requests to unknown routes return 401 (not 404) by design.
- API tests use `Infrastructure/ApiFactory` (test Auth0 settings + local RSA signing key; `ApiFactory.CreateToken(...)`), never a bare `WebApplicationFactory<Program>` — the API won't start without Auth0 settings.
- Authorization: Auth0 RBAC supplies `permissions` (`<action>:<resource>`) in the access token; the API checks permissions via named policies, never role names. Record-level rules (ownership etc.) live in the API.
- Permissions/roles are defined only in `infra/auth0/permissions.json`. Never hand-type permission strings; use the generated `Permissions.g.cs` (never edit `*.g.*` files). Expose permission names in OpenAPI so the frontend can generate its types.
- Roles: `member` (default, auto-assigned on first login) and `admin` (includes all `member` permissions — Auth0 roles don't inherit). A user with no role gets 403.
- Identify the caller only from the token `sub` claim. A user's own data is accessed via `/users/me`, never by a client-supplied id.
- Auth0 is the identity source of truth; the local `Users` table stores only app-specific data keyed by `Auth0UserId` (`sub`).
- Auth0 changes go through Terraform, not the dashboard (only exception: promoting real users to `admin`). Never commit `*.tfstate*`, `.terraform/`, or secret tfvars; do commit `.terraform.lock.hcl`.
- Never commit secrets (Auth0 config, DB passwords). Use Aspire parameters/user-secrets.
- Data Protection keys are in memory only (`AddInMemoryDataProtectionKeys`), since the API protects nothing. This is temporary: a later story replaces it with persisted, encrypted keys as part of a proper auth flow (spec §3.1, §8). Until then, nothing may rely on protected data (cookies, antiforgery, BFF).
- The AppHost is the single source of truth for topology: add resources (database, cache, broker, services) there only. Don't add hand-written Dockerfiles, compose files, or NGINX config — compose files are generated by the Aspire Docker Compose publisher (never edit or commit `aspire-output/`), and the gateway waits until there are multiple APIs.
- Package versions only in `Directory.Packages.props` (no `Version=` on `PackageReference`); shared build settings in `Directory.Build.props`; warnings are errors. Files use LF line endings.
- New projects: create with `dotnet new` under `src/` or `tests/` and add them to `np-aspire-api.slnx`. New test projects: `dotnet new xunit3`, target `net10.0`, set `<IsTestProject>true</IsTestProject>`, reference `coverlet.MTP`, copy `testconfig.json` from the API tests, and add a CI coverage step plus a `codecov.yml` component.
- Coverage minimums (API: lines 80 / branches 75 / methods 80) are enforced by `scripts/coverage-check.mjs`. Never lower a minimum to make CI pass — add tests.
- `main` is protected: all changes go through a PR with passing CI (`Build and test`, `codecov/patch`, `codecov/project`). Never push directly to `main`. Keep the CI job name stable.
- When a TBD in `docs/spec.md` gets decided, update the spec.
