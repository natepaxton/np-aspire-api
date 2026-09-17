# np-aspire-api

Backend for np-aspire: a single .NET 10 API behind an NGINX gateway, orchestrated locally with Aspire, runnable as containers via a hand-written root `compose.yaml`. The Angular frontend is a separate repo: https://github.com/natepaxton/np-aspire

Full spec, architecture, and open decisions: @docs/spec.md

## Stack

- **API:** .NET 10 (LTS) ASP.NET Core (controllers), PostgreSQL, EF Core + Dapper (Npgsql), OpenAPI (the contract np-aspire generates its client from)
- **Auth:** Auth0 — the API validates JWT access tokens; login/tokens are handled by Auth0 and the SPA
- **Orchestration:** Aspire 13 AppHost for local development; hand-written root `compose.yaml` for the container stack (API image from `src/NpAspire.Api/Dockerfile`)
- **Gateway:** NGINX (`nginx-unprivileged`, config `infra/nginx/default.conf`): `/api/` → API, `/` → frontend static-site container (placeholder until it exists)
- **Auth0 config:** Terraform (`infra/auth0/`, `auth0/auth0` provider), driven by `infra/auth0/permissions.json`
- **Tests:** xUnit v3 on Microsoft Testing Platform, `Microsoft.AspNetCore.Mvc.Testing`, `coverlet.MTP` + ReportGenerator
- **CI:** GitHub Actions (`.github/workflows/ci.yml`)
- **Deferred:** microservices split, RabbitMQ, BFF auth (see spec §8)

## Layout

- `src/NpAspire.Api` the API; `src/NpAspire.AppHost` Aspire AppHost; `src/NpAspire.ServiceDefaults` shared Aspire defaults
- `tests/NpAspire.Api.Tests` API tests
- `infra/nginx/` gateway config; `infra/auth0/` Terraform + permissions manifest (`.env.local` there is gitignored and holds the Terraform credentials)
- `compose.yaml`, `.dockerignore` (root); `src/NpAspire.Api/Dockerfile`
- `scripts/` repo scripts (`coverage-check.mjs`, `compose-smoke.sh`)

## Commands

- After cloning: `dotnet tool restore` (ReportGenerator, pinned in `dotnet-tools.json`)
- Build: `dotnet build np-aspire-api.slnx`
- Test: `dotnet test --solution np-aspire-api.slnx`
- Test with coverage + minimums (as CI does): `dotnet build np-aspire-api.slnx && node scripts/coverage-check.mjs --project tests/NpAspire.Api.Tests --out coverage --lines 80 --branches 75 --methods 80`
- Format: `dotnet format np-aspire-api.slnx` (CI runs `--verify-no-changes`)
- Run everything (dev): `dotnet run --project src/NpAspire.AppHost` (the AppHost SDK bundles what it needs)
- Aspire CLI (optional, machine-wide — not in the tool manifest, see spec §3.3): `curl -sSL https://aspire.dev/install.sh | bash`; then `aspire run`, `aspire describe`
- Full stack in containers: `docker compose up --build --detach --wait` → http://localhost:8080 (`/api/...`); stop with `docker compose down --volumes`
- Smoke test the running stack (as CI does): `./scripts/compose-smoke.sh`
- Build only the API image: `docker build -f src/NpAspire.Api/Dockerfile -t np-aspire-api .` (context is the repo root)
- Auth0 (dev tenant): `set -a; source infra/auth0/.env.local; set +a`, then `terraform -chdir=infra/auth0 plan -var-file=env/dev.tfvars` / `apply` (spec §5.2)

## Rules

- API routes are `api/v{version}/<plural-resource>` via `Asp.Versioning.Mvc`; controllers are plural (`UsersController`). NGINX forwards `/api/` paths unchanged and knows nothing about versions.
- EF Core for writes and migrations, Dapper for read-heavy queries.
- The API validates Auth0 JWTs itself; NGINX only forwards the `Authorization` header. Auth0 owns login/tokens — don't build login endpoints in the API.
- API is deny-by-default (fallback policy requires auth); opt out explicitly with `[AllowAnonymous]`.
- Authorization: Auth0 RBAC supplies `permissions` (`<action>:<resource>`) in the access token; the API checks permissions via named policies, never role names. Record-level rules (ownership etc.) live in the API.
- Permissions/roles are defined only in `infra/auth0/permissions.json`. Never hand-type permission strings; use the generated `Permissions.g.cs` (never edit `*.g.*` files). Expose permission names in OpenAPI so the frontend can generate its types.
- Roles: `member` (default, auto-assigned on first login) and `admin` (includes all `member` permissions — Auth0 roles don't inherit). A user with no role gets 403.
- Identify the caller only from the token `sub` claim. A user's own data is accessed via `/users/me`, never by a client-supplied id.
- Auth0 is the identity source of truth; the local `Users` table stores only app-specific data keyed by `Auth0UserId` (`sub`).
- Auth0 changes go through Terraform, not the dashboard (only exception: promoting real users to `admin`). Never commit `*.tfstate*`, `.terraform/`, or secret tfvars; do commit `.terraform.lock.hcl`.
- Never commit secrets (Auth0 config, DB passwords). Use Aspire parameters/user-secrets in dev, `.env` for compose.
- `compose.yaml` is hand-written and mirrors the AppHost: when a resource/service is added to one, add it to the other in the same PR. Keep images pinned to exact tags (Dependabot updates them); keep containers non-root, read-only, and `no-new-privileges`.
- Only the gateway publishes a host port. The API is internal (`expose`), and health checks outside Development are served only on the management port 8081 (`HealthChecks:ManagementPort`), which the gateway never routes to. The chiseled image has no shell/curl — container health checks use `dotnet NpAspire.Api.dll --health-check <url>`.
- Dockerfile: multi-stage, restore layer first (copy props + csproj, then restore, then sources), build context = repo root; update `.dockerignore` when adding top-level folders.
- Package versions only in `Directory.Packages.props` (no `Version=` on `PackageReference`); shared build settings in `Directory.Build.props`; warnings are errors. Files use LF line endings.
- New projects: create with `dotnet new` under `src/` or `tests/` and add them to `np-aspire-api.slnx`. New test projects: `dotnet new xunit3`, target `net10.0`, set `<IsTestProject>true</IsTestProject>`, reference `coverlet.MTP`, copy `testconfig.json` from the API tests, and add a CI coverage step plus a `codecov.yml` component.
- Coverage minimums (API: lines 80 / branches 75 / methods 80) are enforced by `scripts/coverage-check.mjs`. Never lower a minimum to make CI pass — add tests.
- `main` is protected: all changes go through a PR with passing CI (`Build and test`, `Compose smoke test`, `codecov/patch`, `codecov/project`). Never push directly to `main`. Keep the CI job names stable.
- When a TBD in `docs/spec.md` gets decided, update the spec.
