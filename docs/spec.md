# np-aspire-api — Project Spec

Status: early draft. Sections marked **TBD** are open decisions.

This repository holds the **backend** of np-aspire: the .NET API, the Aspire AppHost that orchestrates it, and the Auth0 configuration. The Angular frontend lives in **[np-aspire](https://github.com/natepaxton/np-aspire)**.

The two repositories were one Nx monorepo until 2026-09-17. The .NET history was carried over with `git filter-repo`. The combined state is tagged `pre-api-split` in np-aspire.

## 1. Goal

A .NET API orchestrated by **Aspire, the single source of truth** for the backend's topology. There are no hand-written Docker or compose files. Deployment files are generated from the AppHost by Aspire's **Docker Compose** publisher (decision 2026-09-17, §3.2).

The backend starts as **one API** with no gateway in front of it (decision 2026-09-17). An NGINX gateway is deferred until there are multiple APIs to route (§8). The `/api/v1/` route prefix stays, so a gateway can be added later without changing clients.

## 2. Architecture

```
 Browser ──── login ────► Auth0
    │  HTTP + Auth0 access token
    ▼
 API (.NET, validates JWTs)      ◄── Aspire AppHost (dev orchestration + dashboard)
    │
    ▼
 PostgreSQL (milestone 3)
```

- **Routing:** the API owns its full route, including the version: `/api/v1/...`.
- **Local development:**
  - `dotnet run --project src/NpAspire.AppHost` starts the API, and PostgreSQL from milestone 3, with the Aspire dashboard.
  - The frontend runs separately with `nx serve` in np-aspire. Its dev-server proxy forwards `/api` to the API's HTTP endpoint (`http://localhost:5104`), so the browser sees one origin and no CORS setup is needed in development.
- **Production hosting** (same-origin reverse proxy, or CORS on the API) and TLS termination are **TBD** (§7). They will be decided together with the deployment target.

## 3. Components

### 3.1 API

- .NET 10 (LTS), or the newest LTS release at the time.
- One ASP.NET Core Web API project (`NpAspire.Api`) using **controllers**.
- Routes are prefixed `api/v{version}/` and versioned with `Asp.Versioning.Mvc`. Everything starts at v1.
- Database: **PostgreSQL**.
- Data access:
  - **EF Core** (Npgsql provider) for writes, migrations, and domain persistence.
  - **Dapper** (Npgsql) for read-heavy or performance-sensitive queries.
- Auth: the API validates **Auth0** access tokens itself (§4).
- There is no HTTPS redirection for now. How TLS is terminated is decided with the hosting target (§7).
- **Data Protection keys are kept in memory** (`AddInMemoryDataProtectionKeys`, `DataProtection/`). The API protects nothing, because it uses bearer tokens only (no cookies, sessions, or antiforgery). ASP.NET Core still creates a key at startup, and by default writes it unencrypted to the container's disk, which logged two warnings on every deployed start.
  - The keys are regenerated on every start and differ between instances.
  - Before anything relies on protected data, such as BFF cookie auth (§8), persist the keys to shared storage (for example PostgreSQL) and encrypt them with a certificate.
- OpenAPI is served in Development (`/openapi/v1.json`). It is the **contract for the frontend**: np-aspire generates its API client from it.

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
| `DiagnosticsController` | `GET /api/v1/diagnostics` | anonymous ✅                          | Diagnostics: overall health as a `ServerResult<int>`; 200, or 503 when unhealthy. |
| `AuthController`  | `GET /api/v1/auth/check` | any authenticated caller ✅                | Diagnostics: 200 with a valid token, else 401. |
| `UsersController` | `GET /api/v1/users/me`   | `read:profile`                             | Get or provision the caller's record.  |
| `UsersController` | `PUT /api/v1/users/me`   | `update:profile`                           | Update the caller's app-specific data. |
| `UsersController` | `GET /api/v1/users/{id}` | `read:users` permission (the `admin` role) | Admin lookup of any user.              |

- A user's own data is always reached through `/me`. The user is identified from the token's `sub`, never from an id the client sends. This prevents one user reading another user's data by changing the id (an IDOR vulnerability).
- In a browser app, the API cannot tell "the app" apart from "the user": every request carries the user's token, and the user can replay it with any HTTP tool. Rules must therefore be written in terms of _what this user may access_. For a user's own data that is `/me`. `/users/{id}` is admin-only.
- `GET /users/me` also returns the caller's `permissions`, taken from the validated token, so the UI can show or hide admin features.

`DiagnosticsController` is a public status check (`CheckStatus`). It runs every registered health check (`HealthCheckService`) and returns a `ServerResult<int>` whose `Data` is the overall `HealthStatus` (0 Unhealthy, 1 Degraded, 2 Healthy). Unhealthy returns 503; Degraded returns 200 with a warning. Because it is anonymous, it returns only the overall status, never check names or details. Health checks registered by Aspire client integrations (such as the PostgreSQL one in milestone 3) are included automatically. If the checks can't run at all, it returns 500 with the exception's message in `ErrorMessages`. The stack trace is added to `StackTrace` only in Development (`AddError(exception, includeStackTrace: …)`), because stack traces reveal code structure.

**Response envelope:** `ServerResult<T>` (`src/NpAspire.Api/Common/`) wraps a call's `Data` with its `StatusCode` and the `ErrorMessages`, `StackTrace`, `WarningMessages`, and `SuccessMessages` collected while handling it. Related extension methods and enums go in the same folder.

`AuthController` is for **diagnostics only**: it lets you confirm that a token is accepted, for example from Postman, Rider, or the frontend. It has **no login, logout, or token endpoints**. Auth0 handles those directly with the Angular app.

The `api/v1` prefix is applied to every controller by an MVC convention (`Routing/RoutePrefixConvention`, registered in `Program.cs`), so controllers declare only their resource, for example `[Route("diagnostics")]`. An absolute template (`/…` or `~/…`) opts a controller out. Non-controller endpoints (`/health`, `/alive`, `/openapi/v1.json`) are not prefixed. When `Asp.Versioning.Mvc` is added in milestone 3, the prefix becomes `api/v{version:apiVersion}`.

### 3.2 Orchestration (Aspire)

- **Aspire AppHost** (`NpAspire.AppHost`) is the **single source of truth** for the backend topology: the API now, and PostgreSQL from milestone 3. Every new resource (database, cache, message broker, service) is added to the AppHost and nowhere else.
- The shared **ServiceDefaults** project (`NpAspire.ServiceDefaults`) supplies OpenTelemetry, health checks, service discovery, and resilience.
- **No Docker or compose files are maintained by hand.** A Dockerfile, a hand-written `compose.yaml`, and an NGINX gateway were tried in PR #2 and closed unmerged; the branch `feature/docker-compose` is kept for reference.
  - When containers are needed, the API image comes from the .NET SDK's container support (`dotnet publish /t:PublishContainer`), which Aspire uses.
  - Deployment artifacts come from the AppHost through an Aspire publisher (`aspire publish`).
- **Deployment target: Docker Compose** (decision 2026-09-17).
  - The AppHost declares a Docker Compose environment (`builder.AddDockerComposeEnvironment("env")`, package `Aspire.Hosting.Docker`).
  - `aspire publish` writes `docker-compose.yaml` and `.env` files to `src/NpAspire.AppHost/aspire-output/`. The output is generated, so it is gitignored and never edited by hand. Regenerate it after changing the AppHost.
  - The generated compose file includes an Aspire dashboard (`env-dashboard`) that receives the API's telemetry. The API image (`API_IMAGE`) and the Auth0 settings (`AUTH0_DOMAIN`, `AUTH0_AUDIENCE`) are supplied through the `.env` values.
  - Where the compose stack runs, and how TLS is terminated, is still open (§7).
- Secrets such as Auth0 settings and the database password come from Aspire parameters or user-secrets. Never commit them.

### 3.3 .NET setup

- `np-aspire-api.slnx`: the solution, with `/src/` and `/tests/` folders.
- `global.json` pins SDK `10.0.401` (`rollForward: latestFeature`) and selects the **Microsoft Testing Platform** runner for `dotnet test`.
- `Directory.Build.props` applies to every project: nullable reference types, implicit usings, warnings as errors, and code style enforced in the build.
- `Directory.Packages.props` holds every NuGet version (central package management). `PackageReference` items have no `Version`.
- `dotnet-tools.json` pins ReportGenerator as a local tool. Run `dotnet tool restore` after cloning.
- The **Aspire CLI** is _not_ in the tool manifest. On Linux, `dotnet tool restore` fails when the manifest contains `aspire.cli`, which ships platform-specific packages: it reports the other tool as containing only `aspire`. This was reproduced in the `mcr.microsoft.com/dotnet/sdk:10.0` image.
  - `dotnet run --project src/NpAspire.AppHost` works without the CLI, because the AppHost SDK bundles it (`AspireUseCliBundle`).
  - For `aspire run`, `describe`, and `publish`, install it machine-wide with `curl -sSL https://aspire.dev/install.sh | bash`. CI will install it the same way if it ever runs `aspire publish`.
- **Tests:** **xUnit v3** on Microsoft Testing Platform, with `Microsoft.AspNetCore.Mvc.Testing` for in-memory integration tests.
  - Test projects set `<IsTestProject>true</IsTestProject>` and reference `coverlet.MTP`. Coverlet settings live in each project's `testconfig.json`.
- ServiceDefaults maps `/health` and `/alive` in Development only. Tests verify that they, and the OpenAPI document, return 404 in Production.
- Line endings are LF everywhere (`.gitattributes`, `.editorconfig`). `dotnet format --verify-no-changes` runs in CI.

## 4. Auth and API security (Auth0)

Flow: the SPA uses **Authorization Code + PKCE** (see np-aspire's spec), and the API validates the resulting **access tokens**.

- An Auth0 **API** (resource server) is registered. Its identifier is the token audience, which makes the SPA's tokens JWT access tokens and not opaque ones.
  - Until Terraform manages the tenant (milestone 2), the API is created by hand in the Auth0 dashboard (Applications → APIs, signing algorithm RS256). Milestone 2 will `terraform import` it.
- **Implemented** in `Authentication/AuthenticationExtensions.cs` (`AddAuth0Authentication`) with `Microsoft.AspNetCore.Authentication.JwtBearer`:
  - Configuration comes from the `Auth0` section, bound to `Auth0Options` (`Domain`, `Audience`). It is validated at startup (`ValidateOnStart`), so the API refuses to start without it.
  - `Authority = https://<Domain>/`. A leading `https://` or trailing `/` in `Domain` is tolerated.
  - `Audience = <Audience>`.
  - The handler validates the signature (keys come from the tenant's JWKS through OpenID metadata), issuer, audience, and lifetime (default 5-minute clock skew).
  - Only **RS256** is accepted (`ValidAlgorithms`).
  - `MapInboundClaims = false` keeps Auth0's claim names (`sub`, `permissions`), and `NameClaimType = "sub"`.
- **Deny by default (implemented):** a fallback authorization policy requires an authenticated user on every endpoint.
  - Anonymous endpoints opt out explicitly with `AllowAnonymous()`: the Development health checks, the Development OpenAPI document, and `GET /api/v1/diagnostics`.
  - **Anonymous requests to unknown routes get 401, not 404**, so unauthenticated callers can't find out which routes exist. Authenticated callers get 404.
- **Local configuration:** the AppHost defines two Aspire **parameters**, `auth0-domain` and `auth0-audience`, and passes them to the API as `Auth0__Domain` and `Auth0__Audience`. Neither is a secret.
  - Set them once in the AppHost's user secrets:
    ```bash
    dotnet user-secrets set "Parameters:auth0-domain" "<tenant>.us.auth0.com" --project src/NpAspire.AppHost
    dotnet user-secrets set "Parameters:auth0-audience" "<API identifier>" --project src/NpAspire.AppHost
    ```
  - If they're missing, the Aspire dashboard asks for them.
- **Testing without Auth0:** `tests/.../Infrastructure/ApiFactory.cs` hosts the API with test settings and swaps the tenant metadata for a local RSA key through a static OpenID configuration. `ApiFactory.CreateToken(...)` issues valid or deliberately invalid tokens: expired, wrong audience, wrong issuer, untrusted key, HS256, or malformed.
- **Manual testing:** get a token from the Auth0 dashboard (Applications → APIs → your API → **Test** tab). Then call `GET http://localhost:5104/api/v1/auth/check` with `Authorization: Bearer <token>`, using Postman or the `NpAspire.Api.http` request, which reads `accessToken` from the gitignored `http-client.private.env.json`.
- **Permissions** (milestone 3): map the token's `permissions` claim to named policies (for example `[Authorize(Policy = "read:users")]`). See §5.
- Identify the caller only from the `sub` claim.
- Add rate limiting (`Microsoft.AspNetCore.RateLimiting`) to authenticated endpoints (milestone 3).

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
- **Production hosting and TLS:** where the Docker Compose stack (§3.2) and the frontend run, whether they share an origin (a platform reverse proxy) or the API enables CORS for the frontend's origin, and where HTTPS terminates.
- Remote Terraform state backend (needed before CI runs `terraform apply`).

## 8. Deferred

- **In-app role management.** An admin UI that assigns Auth0 roles through the Management API.
- **Backend-for-frontend (BFF)** auth pattern (§4). It needs persisted, encrypted Data Protection keys (§3.1).
- **NGINX gateway.** Add it when there are multiple APIs to route. It will be an AppHost container resource with a `location /api/v1/<resource>/` block per API, so clients keep calling `/api/v1/...`. It will then also forward headers (`UseForwardedHeaders`), terminate TLS, and serve or route the frontend.
- **Deployment pipeline.** The Docker Compose publisher is set up (§3.2). Still deferred: building and pushing the API image, the frontend image, a production host (§7), and running `aspire publish` or deploying from CI.
- **Microservices split.** New services go in `src/<Service>/`, and each owns its own database. It comes together with the NGINX gateway above.
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
  auth0/                      Terraform + permissions.json (milestone 2)
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
   - ✅ Auth0 JWT validation with deny-by-default, and `GET /api/v1/auth/check` (done early, 2026-09-17)
   - permission policies
   - the `Users` table, and `UsersController` (`/me` and the admin `/{id}`)
   - permission names exposed in OpenAPI
   - (From the monorepo's M4, the gateway is deferred (§8). The Docker Compose publisher was added early, 2026-09-17; see §3.2.)
4. ✅ **CI, coverage, Codecov, branch protection, Dependabot, secret scanning** (set up with the repository, 2026-09-17).

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
