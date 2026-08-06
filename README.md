# ShowVault

ShowVault is a cross-platform production-resilience platform for entertainment venues. It discovers production infrastructure, creates verified backup packages, models dependencies, and guides recovery.

## Core workflow

1. Scan
2. Backup
3. Verify
4. Restore

## Repository

- `apps/showvault_app` — Flutter client for macOS, Windows, iOS, and Android
- `services/api` — ASP.NET Core API
- `docs` — current product and engineering record
- `infra` — local infrastructure

## Prerequisites

- Flutter stable
- .NET 9 SDK
- Docker Desktop

## Local development

```bash
cp .env.example .env
docker compose -f infra/docker-compose.yml up -d
dotnet run --project services/api/src/ShowVault.Api
cd apps/showvault_app && flutter pub get && flutter run
```

The repository foundation is intentionally free of secrets. Replace development values in `.env` before connecting external services.
