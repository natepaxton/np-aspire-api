# np-aspire-api — Project Spec

Status: early draft. Sections marked **TBD** are open decisions.

This repository holds the **backend** of np-aspire: the .NET API, the Aspire AppHost that orchestrates the system, the NGINX gateway, the Docker Compose output, and the Auth0 configuration. The Angular frontend lives in **[np-aspire](https://github.com/natepaxton/np-aspire)**.

The two repositories were one Nx monorepo until 2026-09-17. The .NET history was carried over with `git filter-repo`. The combined state is tagged `pre-api-split` in np-aspire.

## 1. Goal

A .NET API with Aspire as the local development orchestrator. A single `docker compose up` runs the whole system (gateway, API, database, and the frontend's static-site image) as containers, using a compose file that Aspire generates.

The backend starts as **one API**. A microservices split is deliberately deferred (see §8). The NGINX gateway and the `/api/` route prefix exist now so that split can happen later without changing clients.

## 2. Architecture

```
 Browser ──── login ────► Auth0
    │  HTTP (same origin) + Auth0 access token
    ▼
 ┌─────────────────────────────────────────────┐
 │                   NGINX                     │
 │  /        → web (np-aspire static image)    │
 │  /api/    → API (path forwarded as-is)      │
 └──────────────┬──────────────────────────────┘
                ▼
          API (.NET, validates JWTs)
                │
                ▼
           PostgreSQL
```

- **All browser traffic goes through NGINX.** Clients never call the API's port directly.
- **Routing:**
  - NGINX sends `/` to the frontend and `/api/` to the API.
  - The API owns its full route, including the version: `/api/v1/...`.
  - NGINX forwards the path unchanged and knows nothing about versions.
- **Same origin:** the frontend and the API share one origin, so production needs no CORS setup.
- **Local development:**
  - The AppHost runs NGINX (on a fixed host port, planned `8080`), the API, and PostgreSQL.
  - The frontend runs separately with `nx serve` in np-aspire. Its dev-server proxy forwards `/api` to the gateway, so dev routing matches production.
- **Frontend in compose:** np-aspire publishes a static-site container image to GHCR (**TBD**, np-aspire milestone). The AppHost references it as a container resource, so the generated compose file includes it.

## 3. Components

### 3.1 API

- .NET 10 (LTS), or the newest LTS release at the time.
- One ASP.NET Core Web API project (`NpAspire.Api`) using **controllers**.
- Routes are prefixed `api/v{version}/` and versioned with `Asp.Versioning.Mvc`. Everything starts at v1.
- Database: **PostgreSQL**.
- Data access:
  - **EF Core** (Npgsql provider) for writes, migrations, and domain persistence.
  - **Dapper** (Npgsql) for read-heavy or performance-sensitive queries.
- Auth: the API validates **Auth0** access tokens itself (§4). NGINX forwards the `Authorization` header and does not validate tokens.
- There is no HTTPS redirection, because TLS terminates at NGINX.
- OpenAPI is served in Development (`/openapi/v1.json`). It is the **contract for the frontend**: np-aspire generates its API client from it.
- The API ships as a Docker image.

**User data model.** Auth0 is the source of truth for identity: credentials, email verification, MFA, and social logins. The API stores only app-specific user data.

| Column                   | Notes                                                                                                                            |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| `Id`                     | `uuid`, primary key. Internal id used for foreign keys.                                                                          |
| `Auth0UserId`            | The token's `sub` claim. Unique index. This is the only link to Auth0.                                                           |
| `Email`, `DisplayName`   | A cached copy of Auth0 claims, for display and queries only. Refreshed on each `/users/me` call. Never treated as authoritative. |
| _app-specific fields_    | Preferences, profile data, and so on. Added as needed.                                                                           |
| `CreatedAt`, `UpdatedAt` | Timestamps.                                                                                                                      |

**Provisioning:** the local record is created just in time. The first authenticated `GET /users/me` inserts the row, and later calls update the cached claims. No Auth0 Action or webhook is needed.

**Endpoints**

| Controller        | Route                    | Authorization                              | Purpose                                |
| ----------------- | ------------------------ | ------------------------------------------ | -------------------------------------- |
| `UsersController` | `GET /api/v1/users/me`   | `read:profile`                             | Get or provision the caller's record.  |
| `UsersController` | `PUT /api/v1/users/me`   | `update:profile`                           | Update the caller's app-specific data. |
| `UsersController` | `GET /api/v1/users/{id}` | `read:users` permission (the `admin` role) | Admin lookup of any user.              |

- A user's own data is always reached through `/me`. The user is identified from the token's `sub`, never from an id the client sends. This prevents one user reading another user's data by changing the id (an IDOR vulnerability).
- In a browser app, the API cannot tell "the app" apart from "the user": every request carries the user's token, and the user can replay it with any HTTP tool. Rules must therefore be written in terms of _what this user may access_. For a user's own data that is `/me`. `/users/{id}` is admin-only.
- `GET /users/me` also returns the caller's `permissions`, taken from the validated token, so the UI can show or hide admin features.

There is **no `AuthController`**. Auth0 handles login, logout, and token issuing and refresh directly with the Angular app.

### 3.2 Orchestration and infrastructure

- **Aspire AppHost** (`NpAspire.AppHost`) is the single source of truth for the system topology: the API, PostgreSQL, NGINX, and the frontend image. The shared **ServiceDefaults** project (`NpAspire.ServiceDefaults`) supplies OpenTelemetry, health checks, service discovery, and resilience.
- **docker-compose is generated by Aspire.** The AppHost uses the Docker Compose publisher (`Aspire.Hosting.Docker`, `AddDockerComposeEnvironment`). Running `aspire publish` writes the compose file and its `.env` to `deploy/compose/`.
  - Never hand-edit the generated files. Change the AppHost and publish again.
  - Because the compose file is generated, NGINX must be modeled in the AppHost as a container resource with its config mounted.
- **NGINX** config lives in `infra/nginx/`:
  - `location /api/` proxies to the API. It keeps the path and sets the `X-Forwarded-*` headers. The API enables `UseForwardedHeaders`.
  - `location /` proxies to the frontend container, which serves the built Angular app with an SPA fallback to `index.html`.
- **TLS:** in production, HTTPS terminates at NGINX. HTTP redirects to HTTPS, and HSTS is enabled. NGINX does not log the `Authorization` header. Plain HTTP is acceptable in local dev only.
- Secrets such as Auth0 settings and the database password come from Aspire parameters or user-secrets in dev and from `.env` for compose. Never commit them.

### 3.3 .NET setup

- `np-aspire-api.slnx`: the solution, with `/src/` and `/tests/` folders.
- `global.json` pins SDK `10.0.401` (`rollForward: latestFeature`) and selects the **Microsoft Testing Platform** runner for `dotnet test`.
- `Directory.Build.props` applies to every project: nullable reference types, implicit usings, warnings as errors, and code style enforced in the build.
- `Directory.Packages.props` holds every NuGet version (central package management). `PackageReference` items have no `Version`.
- `dotnet-tools.json` pins ReportGenerator as a local tool. Run `dotnet tool restore` after cloning.
- The **Aspire CLI** is _not_ in the tool manifest. On Linux, `dotnet tool restore` fails when the manifest contains `aspire.cli`, which ships platform-specific packages: it reports the other tool as containing only `aspire`. This was reproduced in the `mcr.microsoft.com/dotnet/sdk:10.0` image.
  - `dotnet run --project src/NpAspire.AppHost` works without the CLI, because the AppHost SDK bundles it (`AspireUseCliBundle`).
  - For `aspire run`, `describe`, and `publish`, install it machine-wide with `curl -sSL https://aspire.dev/install.sh | bash`.
  - Milestone 4, which runs `aspire publish` in CI, will install it the same way.
- **Tests:** **xUnit v3** on Microsoft Testing Platform, with `Microsoft.AspNetCore.Mvc.Testing` for in-memory integration tests.
  - Test projects set `<IsTestProject>true</IsTestProject>` and reference `coverlet.MTP`. Coverlet settings live in each project's `testconfig.json`.
- ServiceDefaults maps `/health` and `/alive` in Development only. Tests verify that they, and the OpenAPI document, return 404 in Production.
- Line endings are LF everywhere (`.gitattributes`, `.editorconfig`). `dotnet format --verify-no-changes` runs in CI.

## 4. Auth and API security (Auth0)

Flow: the SPA uses **Authorization Code + PKCE** (see np-aspire's spec), and the API validates the resulting **access tokens**.

- An Auth0 **API** (resource server) is registered. Its identifier is the token audience, which makes the SPA's tokens JWT access tokens and not opaque ones.
- JWT bearer auth (`Microsoft.AspNetCore.Authentication.JwtBearer`) with:
  - `Authority = https://<tenant>.auth0.com/`
  - `Audience = <api identifier>`
- The handler validates the signature (via JWKS), issuer, audience, and lifetime.
- **Deny by default:** a fallback authorization policy requires an authenticated user on every endpoint. Anonymous endpoints must opt out explicitly with `[AllowAnonymous]`; health checks are one example.
- **Permissions:** map the token's `permissions` claim to named policies (for example `[Authorize(Policy = "read:users")]`). See §5.
- Identify the caller only from the `sub` claim.
- Add rate limiting (`Microsoft.AspNetCore.RateLimiting`) to authenticated endpoints.

Future hardening (deferred): the **backend-for-frontend (BFF)** pattern. The server handles login and keeps tokens server-side, and the browser holds only an HttpOnly cookie. This is the strongest option for browser apps, but it adds complexity that isn't needed yet.

## 5. Roles and permissions

There are two layers, split by what each system can know.

**1. Auth0 decides what _kind_ of action a user may perform (RBAC).**

- Enable RBAC on the Auth0 API and turn on "Add Permissions in the Access Token".
- **Permissions** are defined on the Auth0 API and named `<action>:<resource>`. **Roles** are bundles of permissions assigned to users.

| Role     | Permissions                                    | Who                                                         |
| -------- | ---------------------------------------------- | ----------------------------------------------------------- |
| `member` | `read:profile`, `update:profile`               | Every standard user. Assigned automatically on first login. |
| `admin`  | `read:profile`, `update:profile`, `read:users` | Administrators.                                             |

- Roles are flat: Auth0 has no role inheritance. `admin` therefore lists every `member` permission explicitly, so an admin needs only one role.
- An authenticated user **with no role** has no permissions and gets `403` everywhere except anonymous endpoints. This is deliberate: it makes a missing role assignment obvious.
- **Automatic assignment:** an Auth0 **Post-Login Action**, managed by Terraform, assigns `member` on a user's first login if the user has no roles. It uses a machine-to-machine client that can only assign roles (`read:roles`, `create:role_members`). The Action's credentials are stored as Action secrets.
- **Naming:** the role is `member`, not `user`. `user` collides with the `User` entity, the `users` resource in permission names (`read:users`), and the generic phrase "authenticated user".
- Add new permissions as features need them, for example `write:users`.

**2. The API decides _which records_ a user may touch (resource-based authorization).**

- Auth0 can't know about app data, so rules like "only the owner may edit this item" are enforced in the API. Use ASP.NET Core resource-based authorization (`IAuthorizationService` with requirements and handlers) or ownership filters in queries.

**Rules**

- **The API checks permissions, not role names.** Roles can be reshuffled in Auth0 without code changes.
- **Never hand-type a permission string.** Every permission comes from the permissions manifest (below).
- **The frontend never decides access.** It gets permissions from `GET /users/me` and uses them only to show or hide UI.
- **Permission changes take effect when a new access token is issued.** Keep the API's access-token lifetime short (about 1 hour), because refresh tokens keep users logged in.
- To promote a real user to `admin`, use the Auth0 dashboard for now. An in-app admin screen for managing roles is deferred.

### 5.1 Permissions manifest (single source of truth)

`infra/auth0/permissions.json` defines every permission and role:

```json
{
  "permissions": [
    { "value": "read:profile", "description": "Read your own profile" },
    { "value": "update:profile", "description": "Update your own profile" },
    { "value": "read:users", "description": "Read any user's profile" }
  ],
  "roles": [
    {
      "name": "member",
      "description": "Standard user",
      "default": true,
      "permissions": ["read:profile", "update:profile"]
    },
    { "name": "admin", "description": "Administrator", "permissions": ["read:profile", "update:profile", "read:users"] }
  ]
}
```

- A generator script (`scripts/generate-permissions`, implementation chosen in milestone 2) writes `src/NpAspire.Api/Authorization/Permissions.g.cs`: C# constants, plus a registration helper that creates one authorization policy per permission.
- Generated files are committed and never edited by hand. CI regenerates them and fails if the output differs from what's committed (`git diff --exit-code`).
- Auth0 itself is configured from the same manifest (§5.2), so the names in the tenant match too.
- `"default": true` marks the role the Post-Login Action assigns. Exactly one role may be the default, and the generator validates this. It also checks that every role permission exists in `permissions`.
- **Frontend types:** the API exposes the permission names in its OpenAPI document (as an enum on the `/users/me` response). np-aspire generates its TypeScript types from that document, so no copy of the manifest lives in the frontend repo. The exact mechanism is **TBD** (§7).

### 5.2 Auth0 configuration as code (Terraform)

Everything goes through the Auth0 Management API. **Decision: Terraform** (`auth0/auth0` provider), in `infra/auth0/`. Alternatives considered: Auth0 Deploy CLI (YAML import/export, no state) and the Auth0 CLI (imperative, good for bootstrapping).

What gets configured:

- the Auth0 API (identifier/audience, RBAC on, permissions in the token, token lifetime)
- permissions and roles, from `jsondecode(file("permissions.json"))`
- the SPA application (callback, logout, and web-origin URLs for dev `http://localhost:4300` and prod)
- refresh token rotation
- the Post-Login role-assignment Action and its trigger binding
- test users (dev tenant only)

**Bootstrap (manual, once per tenant):**

1. In the Auth0 dashboard, go to Applications and create a **Machine to Machine** app named `terraform`.
2. Authorize it for the **Auth0 Management API** and grant scopes:
   - A personal dev tenant can grant all scopes.
   - Prod should grant only what Terraform manages: `*:resource_servers`, `*:clients`, `*:client_grants`, `*:roles`, `*:role_members`, `*:actions`, `*:users`, `read:connections`, `update:connections`.
   - A plan or apply that fails with an "insufficient scope" error names the missing scope.
3. Copy the app's Domain, Client ID, and Client Secret into `infra/auth0/.env.local` (gitignored). `infra/auth0/.env.example` is committed with placeholder values.
   ```
   AUTH0_DOMAIN=dev-xxxx.us.auth0.com
   AUTH0_CLIENT_ID=...
   AUTH0_CLIENT_SECRET=...
   TF_VAR_test_user_password=...
   ```

- The Auth0 quickstart's token-request snippet is **not** added to the codebase. The Terraform provider exchanges the client ID and secret for a Management API token itself.
- CI gets the same values from GitHub secrets.

**Layout:**

- `providers.tf`, `variables.tf`, `outputs.tf`
- `api.tf`: resource server, RBAC, token lifetime
- `rbac.tf`: permissions and roles, from the manifest
- `spa.tf`: SPA client and its URLs
- `actions.tf` and `actions/`: the Post-Login Action and its source
- `test-users.tf`
- `env/<name>.tfvars`: non-secret per-environment values, such as URLs and a `create_test_users` flag. There is one Auth0 tenant per environment (dev now, prod later).

**Test users** (dev tenant only, `create_test_users = true`):

- `test-member@<domain>` with the `member` role
- `test-admin@<domain>` with the `admin` role
- `test-norole@<domain>` with no role, to verify the 403 behavior
- Passwords come from secret variables and never from committed files. np-aspire's Playwright tests use the same credentials (GitHub secrets in that repo's CI).

**Commands** (planned; no Nx in this repo). Load the env file first with `set -a; source infra/auth0/.env.local; set +a`, then:

- `terraform -chdir=infra/auth0 init`
- `terraform -chdir=infra/auth0 fmt -check` and `terraform -chdir=infra/auth0 validate`
- `terraform -chdir=infra/auth0 plan -var-file=env/dev.tfvars`
- `terraform -chdir=infra/auth0 apply -var-file=env/dev.tfvars`

A small wrapper script may replace these in milestone 2.

**State and drift:**

- **State:** a local `terraform.tfstate` for now. It is gitignored because it contains secrets, including test-user passwords and client secrets. Move it to a remote backend before CI applies changes.
- Commit `.terraform.lock.hcl`. Ignore `.terraform/`, `*.tfstate*`, and `*.secret.tfvars`.
- The Auth0 dashboard is read-only for everything Terraform manages. Change the `.tf` files, then plan and apply. The one exception is assigning roles to real users.

## 6. CI and repository settings

**GitHub Actions** (`.github/workflows/ci.yml`) runs on PRs into `main` and on pushes to `main`. A newer push cancels an in-progress run for the same branch.

- **`Build and test` job:**
  1. `dotnet tool restore` and `dotnet restore`, with a NuGet cache
  2. `dotnet format --verify-no-changes`
  3. `dotnet build`
  4. `node scripts/coverage-check.mjs …`: tests with coverage and minimums
  5. ReportGenerator's Markdown summary written to the job summary
  6. Upload `coverage/cobertura.xml` to Codecov, and upload the coverage directory as an artifact
- **Later additions:**
  - `generate-permissions` drift check and `terraform fmt`/`validate` (milestone 2)
  - Docker image build and an `aspire publish` check (milestone 4)
  - `terraform plan` PR comment

**Code coverage**

| Scope                                          | Lines | Branches | Methods |
| ---------------------------------------------- | ----- | -------- | ------- |
| API (`NpAspire.Api`)                           | 80%   | 75%      | 80%     |
| Changed lines in a PR (Codecov `patch` status) | 80%   | —        | —       |

- `coverlet.MTP` collects coverage (`dotnet test --coverlet`). ReportGenerator merges it into `coverage/cobertura.xml` and `coverage/Summary.json`.
- Neither tool can enforce a minimum on Microsoft Testing Platform, so `scripts/coverage-check.mjs` runs both and fails below the minimums. The same command runs locally and in CI.
- The merged file is named lowercase `cobertura.xml`, because Codecov's file search is case-sensitive.
- **Excluded from coverage:**
  - `NpAspire.AppHost` and `NpAspire.ServiceDefaults` (only `[NpAspire.Api]*` is instrumented)
  - EF migrations and generated `*.g.cs`
  - `Program.cs` is **not** excluded: the in-memory integration tests run it, so its setup is covered.
- **Codecov (`codecov.yml`):**
  - `project` status: overall coverage may not drop by more than 1%.
  - `patch` status: changed lines must reach 80%.
  - One component per deployable project (`api`).
  - Uploads use the `CODECOV_TOKEN` secret, stored as both an Actions secret and a Dependabot secret.

**Branch protection on `main`** (public repository, applied with `gh api`):

- Changes go through a PR. No approvals are required, because this is a solo project; stale reviews are dismissed.
- Required checks: `Build and test`, `codecov/patch`, and `codecov/project`.
- The branch must be up to date with `main` before merging.
- Review conversations must be resolved.
- No force pushes or deletions.
- The rules apply to admins too.

**Secret scanning:** secret scanning and push protection are enabled.

**Dependabot**

- **Alerts and security updates:** enabled in the repository settings.
- **Version updates** (`.github/dependabot.yml`):
  - Checked every Monday at 06:00 America/New_York.
  - New NuGet releases wait 3 days before being proposed (7 days for major releases).
  - Ecosystems:
    - `nuget`: `Directory.Packages.props`, the AppHost SDK version, and `dotnet-tools.json`. Grouped as `aspire`, `aspnetcore-and-extensions`, `opentelemetry`, `testing`, and `minor-and-patch`.
    - `dotnet-sdk`: `global.json`, excluding major versions.
    - `github-actions`
    - Add `terraform` in milestone 2.

## 7. Open decisions

- How np-aspire consumes the API contract and permission types: an OpenAPI client generator, and whether it reads the document from a committed file, a release artifact, or a running API.
- The frontend static-site image: its name and tag scheme on GHCR, and how the AppHost pins its version.
- Remote Terraform state backend (needed before CI runs `terraform apply`).

## 8. Deferred

- **In-app role management.** An admin UI that assigns Auth0 roles through the Management API.
- **Backend-for-frontend (BFF)** auth pattern (§4).
- **Microservices split.** New services go in `src/<Service>/`, and each owns its own database. NGINX gets one new `location /api/v1/<resource>/` block per service. Clients don't change.
- **RabbitMQ.** Deferred until there is a second service or a background-work need. When added, use `RabbitMQ.Client` directly, wrapped in a small shared messaging library in `src/`.

## 9. Repository layout

```
src/
  NpAspire.Api/               ASP.NET Core API
  NpAspire.AppHost/           Aspire AppHost
  NpAspire.ServiceDefaults/   Shared Aspire service defaults
tests/
  NpAspire.Api.Tests/         xUnit v3 (Microsoft Testing Platform)
infra/
  nginx/                      NGINX gateway config (milestone 4)
  auth0/                      Terraform + permissions.json (milestone 2)
deploy/
  compose/                    Generated by `aspire publish`. Do not edit. (milestone 4)
scripts/
  coverage-check.mjs          Tests with coverage + minimums
docs/
  spec.md
.github/
  workflows/ci.yml            CI
  dependabot.yml              Dependency updates
np-aspire-api.slnx            .NET solution
Directory.Build.props         Shared MSBuild settings
Directory.Packages.props      Central NuGet package management
global.json                   SDK pin + Microsoft Testing Platform runner
dotnet-tools.json             Local .NET tools (ReportGenerator)
codecov.yml
.editorconfig / .gitattributes
```

## 10. Milestones

Numbering is new to this repository. The equivalent milestone in the original monorepo plan is shown in brackets.

1. ✅ **API skeleton and Aspire** [monorepo M2] (done 2026-09-17). Details below.
2. **Permissions and Auth0** [part of monorepo M3]:
   - `permissions.json` and the generator, with a CI drift check
   - Terraform for the dev tenant: API, roles, SPA client, Post-Login Action, test users
   - Dependabot `terraform` ecosystem, and `terraform fmt`/`validate` in CI
3. **API features** [rest of monorepo M3]:
   - PostgreSQL (Aspire resource), EF Core, Dapper
   - API versioning
   - Auth0 JWT validation with deny-by-default and permission policies
   - the `Users` table, and `UsersController` (`/me` and the admin `/{id}`)
   - permission names exposed in OpenAPI
4. **Gateway and containers** [monorepo M4]:
   - NGINX as an Aspire resource on a fixed dev port
   - a Dockerfile for the API
   - the frontend image as a container resource
   - the Aspire Docker Compose publisher, so `docker compose up` runs the full stack
   - Aspire CLI install and an `aspire publish` check in CI
5. ✅ **CI, coverage, Codecov, branch protection, Dependabot, secret scanning** (set up with the repository, 2026-09-17).

### Milestone 1 — API skeleton and Aspire ✅

- **Solution** (`np-aspire-api.slnx`):
  - `NpAspire.Api`: controllers-based API skeleton with ServiceDefaults and OpenAPI; no HTTPS redirection
  - `NpAspire.Api.Tests`: 7 in-memory tests covering health, alive, and OpenAPI in Development, 404s in Production, and an unknown route
  - `NpAspire.AppHost`: Aspire 13.5.4; runs `api` with an HTTP health check
  - `NpAspire.ServiceDefaults`
- **Local run:** `dotnet run --project src/NpAspire.AppHost` starts the dashboard, and `api` reports Running/Healthy.
- **Coverage:** 100% lines, branches, and methods. The threshold failure was checked before the Production-environment test was added.
- **Deviations from the original plan:**
  - The Coverlet MSBuild threshold was dropped. .NET 10.0.4xx and xUnit v3 4.x default to Microsoft Testing Platform, where Coverlet can't enforce minimums.
  - The project was built inside the Nx monorepo with `@nx/dotnet`. After the split this repository uses plain `dotnet`, and the projects moved from `apps/api/{src,tests}` and `aspire/{AppHost,ServiceDefaults}` to `src/` and `tests/`.
