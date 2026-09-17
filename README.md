# np-aspire-api

[![CI](https://github.com/natepaxton/np-aspire-api/actions/workflows/ci.yml/badge.svg)](https://github.com/natepaxton/np-aspire-api/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/natepaxton/np-aspire-api/graph/badge.svg)](https://codecov.io/gh/natepaxton/np-aspire-api)

Backend for np-aspire: a .NET 10 API, orchestrated locally with [Aspire](https://aspire.dev). The Angular frontend lives in [np-aspire](https://github.com/natepaxton/np-aspire).

## Prerequisites

- .NET SDK 10.0.401 or later 10.0.4xx (see `global.json`)
- Docker (for Aspire resources such as PostgreSQL)
- Node.js (for `scripts/coverage-check.mjs`)
- Optional: the [Aspire CLI](https://aspire.dev) (`curl -sSL https://aspire.dev/install.sh | bash`)

## Getting started

```bash
dotnet tool restore
dotnet build np-aspire-api.slnx
dotnet run --project src/NpAspire.AppHost   # starts the Aspire dashboard and the API
```

Run the tests with coverage (the same check CI runs):

```bash
node scripts/coverage-check.mjs --project tests/NpAspire.Api.Tests --out coverage --lines 80 --branches 75 --methods 80
```

See [`docs/spec.md`](docs/spec.md) for the architecture, decisions, and milestones.
