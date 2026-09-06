# AgentSquad — Sandbox-Verified Dependency Repair

AgentSquad is a deliberately narrow autonomous software-maintenance team built for the Nebius x NVIDIA Global AI Hackathon's Coding and Agentic Engineering track.

It finds one confirmed vulnerable dependency, applies only the approved patch in a **Nebius Token Factory Sandbox**, runs tests, rescans for the advisory, and shows the evidence in a small web console. It never edits the host workspace, Git, pull requests, or deployments.

## Nebius and NVIDIA

- **Nebius Token Factory** serves `nvidia/nemotron-3-super-120b-a12b` for the short, evidence-grounded risk explanation.
- **Nebius Token Factory Sandboxes** execute the staged manifest change, `dotnet test`, and post-patch vulnerability scan in isolation.
- **Contree MCP** is the constrained Sandbox adapter. It exposes only image import, fixture sync, manifest staging, fixed commands, and evidence retrieval—not arbitrary shell access.

The model does not choose the patch or commands. Deterministic policy permits exactly one direct dependency in the controlled application manifest: `Microsoft.Extensions.Caching.Memory` **8.0.0 → 8.0.1** for `GHSA-qj66-m88j-hmgj`.

## The maintenance squad

```text
Maintenance Coordinator
  ├─ Dependency Sentinel: fixture + NuGet + GHSA evidence
  ├─ Policy Gate: exact package/version/advisory validation
  └─ Patch & Verify: Sandbox patch → test → rescan → evidence
```

An unexpected package, version, or advisory ends as `Blocked`. A Sandbox, test, or rescan failure ends as `Failed`. `Remediated` means only that the patch was verified in a Sandbox.

## Start locally

Prerequisites: .NET 10 SDK, a Nebius Token Factory API key, and an authenticated local `contree-mcp` installation. Obtain and store Sandbox IAM/project credentials separately from the inference key; never put either secret in source control.

```powershell
git clone https://github.com/piechockidaniel/agentsquad-nebius-maintenance.git
cd agentsquad-nebius-maintenance
dotnet dev-certs https --trust
dotnet user-secrets set --project src/AgentSquad.Host "AgentSquad:TokenFactory:ApiKey" "<Token Factory key>"
dotnet user-secrets set --project src/AgentSquad.Host "AgentSquad:Maintenance:Enabled" "true"
dotnet run --project src/AgentSquad.Host
```

Open `https://localhost:53245`, press **Check Nebius preflight**, then run the repair only when preflight is ready. The default scheduled scan is weekdays at 07:30 UTC.

The Token Factory key can instead be supplied as the runtime environment variable `NEBIUS_TOKEN_FACTORY_API_KEY`. The app never logs it. The separate Contree credentials are consumed by `contree-mcp` according to its Nebius configuration.

In the Development launch profile, the maintenance API is open for local iteration. A deployed production container requires a separate 32+ character `AgentSquad__Web__AccessToken`; enter it in the console before using the maintenance APIs. This token is neither a Nebius credential nor an approval mechanism—it prevents an unknown visitor to a public demo URL from starting a Sandbox run.

## Deploy on Nebius

AgentSquad is ready to run as a Docker image on a CPU **Nebius Container VM**. The image runs as a non-root user, exposes port `8080`, performs a Docker health check, and installs the pinned ConTree MCP runtime. Its image build runs the solution tests before publishing the host.

See the operator-ready [Nebius deployment guide](deploy/nebius/README.md) for the Container VM steps, exact Docker arguments, credential boundaries, smoke test, and the short list of values to collect. Start with [runtime.env.example](deploy/nebius/runtime.env.example); it contains placeholders only and must never be populated or committed.

## Verify without cloud credentials

```powershell
dotnet build AgentSquad.sln
dotnet test --solution AgentSquad.sln --no-build
dotnet test samples/maintenance-lab/scenarios/cache-repair/tests/MaintenanceLab.Tests.csproj
```

The `cache-repair` scenario intentionally produces NuGet warning `NU1903` before a repair; that is the vulnerability the Sandbox flow must remove from its staged copy. See the [Maintenance Lab](samples/maintenance-lab/README.md) for the three controlled scenarios.

## API

- `GET /health`
- `GET /maintenance/preflight`
- `GET /maintenance/scenarios`
- `POST /maintenance/runs?scenario=<id>` — returns `202`; only one run may be active (`409` otherwise)
- `GET /maintenance/runs`
- `GET /maintenance/runs/{id}`

All `/maintenance/*` endpoints require the `X-AgentSquad-Access-Token` header in Production. `GET /health` stays unauthenticated so Nebius and external uptime checks can establish that the container is alive.

## Three-minute demo

1. Show the preflight: Token Factory model availability and Contree Sandbox connectivity.
2. Select **Known dependency repair** and run it. Show `GHSA-qj66-m88j-hmgj`, the manifest-only diff, passing baseline and patched tests, clean rescan, and Sandbox workspace snapshot ID.
3. Select **Unapproved second dependency**. It ends `Blocked` at the policy gate and never requests a Sandbox mutation.
4. Select **Baseline test failure**. Its intentional pre-existing regression ends `Failed` before the patch is staged.
5. End on the successful run: `Remediated — Sandbox-only remediation verified`; no host repository or deployment changed.

## Project layout

```text
src/AgentSquad.Host/                  web console and HTTP API
src/AgentSquad.Agents/                coordinator, policy, Sentinel, and scheduler
src/AgentSquad.Plugin.Tools.NebiusSandbox/  constrained Contree MCP adapter
samples/maintenance-lab/              source-owned success and fail-closed .NET 8 demo scenarios
tests/AgentSquad.Tests.Unit/          policy and run-state tests
```

MIT License. See [LICENSE](LICENSE).
