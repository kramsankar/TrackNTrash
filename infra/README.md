# Infrastructure, IaC & Runbook — Module 13

Deployment package for the whole system: Bicep, environment parameters, GitHub Actions, and the operations runbook.

## Contents

| Path | Role |
|------|------|
| `bicep/main.bicep` | Full Azure footprint (RG scope) |
| `bicep/params.{dev,uat,prod}.json` | Per-environment parameters |
| `RUNBOOK.md` | Provisioning, commissioning, go-live, rollback, monitoring |
| `../.github/workflows/*.yml` | CI + deploy workflows (must live under `.github/workflows`) |

## What `main.bicep` provisions

IoT Hub · Azure SQL (+DB) · App Service (Tracking API) · Function App (ingest/D365/asset) · Service Bus (topics `exceptions`, `tracking-events`; queues `fno-business-events`, `d365-repair`) · Storage (containers `exceptions`, `pass-samples`, `pod` with lifecycle: exceptions 1y, pass-samples 30d) · Key Vault (RBAC) · Log Analytics + Application Insights. Managed identities get Key Vault secret-read and Storage blob access via role assignments.

Prod differs from dev/uat by SKU (P1v3 app plan, S3 SQL, ZRS storage), Key Vault purge protection, and 90-day log retention.

## Deploy

```bash
# validate
az bicep build --file bicep/main.bicep --stdout > /dev/null
az deployment group what-if -g rg-tracktrash-dev -f bicep/main.bicep -p @bicep/params.dev.json
# deploy
az deployment group create -g rg-tracktrash-dev -f bicep/main.bicep -p @bicep/params.dev.json
```

Or use the **Deploy Infra** workflow (OIDC login, what-if, gated approvals for uat/prod).

## Workflows

| Workflow | Trigger | Does |
|----------|---------|------|
| `ci.yml` | PR / push to main | .NET tests (label, tracking, d365), asset-api build, Python vision tests, console build, Bicep validate |
| `deploy-infra.yml` | manual (env) | what-if + deploy Bicep |
| `deploy-services.yml` | manual (env) | publish Tracking API + Functions, idempotent DB migrate |
| `deploy-edge.yml` | manual (env, tag) | ACR build/push dock image, update IoT Edge deployment |

CI uses the solution files `LabelApi.sln`, `TrackingApi.sln`, `D365Integration.sln`.

> Full operational detail — camera commissioning, label-printer setup, go-live cutover, rollback, monitoring, routine jobs — is in [`RUNBOOK.md`](RUNBOOK.md).
