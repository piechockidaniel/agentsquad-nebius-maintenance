# AgentSquad — Sandbox-Verified Dependency Repair

AgentSquad is a deliberately narrow autonomous software-maintenance team built for the Nebius x NVIDIA Global AI Hackathon's Coding and Agentic Engineering track.

It finds one confirmed vulnerable dependency, applies only the approved patch in a **Nebius Token Factory Sandbox**, runs tests, rescans for the advisory, and shows the evidence in a small web console. It never edits the host workspace, Git, pull requests, or deployments.

## Nebius and NVIDIA

- **Nebius Token Factory** serves `nvidia/nemotron-3-super-120b-a12b` for the short, evidence-grounded risk explanation.
- **Nebius Token Factory Sandboxes** execute the staged manifest change, `dotnet test`, and post-patch vulnerability scan in isolation.
- **Contree MCP** is the constrained Sandbox adapter. It exposes only image import, fixture sync, manifest staging, fixed commands, and evidence retrieval—not arbitrary shell access.
- **Tavily Search** optionally supplies up to three bounded supplementary security sources after the deterministic policy confirms an advisory. It cannot approve or alter a repair.

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

Prerequisites: .NET 10 SDK, a Nebius Token Factory API key, and an authenticated local `contree-mcp` installation. Obtain and store Sandbox IAM/project credentials separately from the inference key; never put either secret in source control. To include the optional Tavily bonus-evidence path, also obtain a Tavily API key.

If PowerShell reports that `contree-mcp` is not recognized, install the pinned runtime and verify that it is on `PATH` before starting AgentSquad:

```powershell
uv tool install --force --with "mcp==1.9.4" contree-mcp==0.2.0
Get-Command contree-mcp
```

AgentSquad starts that command itself as an MCP stdio process; do not start it separately. The explicit `mcp==1.9.4` pin is required because ConTree 0.2.0 is incompatible with MCP Python SDK 2.x. If you use a different installation location, set `NebiusSandbox__Command` to the full path of its executable for the terminal session.

```powershell
git clone https://github.com/piechockidaniel/agentsquad-nebius-maintenance.git
cd agentsquad-nebius-maintenance
dotnet dev-certs https --trust
dotnet user-secrets set --project src/AgentSquad.Host "AgentSquad:TokenFactory:ApiKey" "<Token Factory key>"
dotnet user-secrets set --project src/AgentSquad.Host "AgentSquad:Tavily:ApiKey" "<Tavily key>"
dotnet user-secrets set --project src/AgentSquad.Host "AgentSquad:Maintenance:Enabled" "true"
dotnet run --project src/AgentSquad.Host
```

Open `https://localhost:53245`. **Step 1 — Preflight** begins automatically and the three provider badges start grey, turn orange while each probe is running, then show green when the connection is ready, red when a required service fails, or amber when the optional Tavily research key is absent. Once NVIDIA Token Factory and the Nebius Sandbox are ready, the three scenario cards open in **Step 2**; start one directly with its own **Run Path** button. The final step shows the live evidence only after a run starts. The default scheduled scan is weekdays at 07:30 UTC.

Preflight uses a bounded, Polly-style browser resilience guard: each request has a 12-second limit; transient connection failures retry at 1, 2, 4, and 8 seconds (five probes total); then the console opens a 30-second circuit cooldown and sends no further requests. The active probe number and cooldown are visible in the console. Configuration and access failures are shown immediately rather than retried. Use **Check again** or **Retry connection** after correcting configuration.

The Token Factory key can instead be supplied as the runtime environment variable `NEBIUS_TOKEN_FACTORY_API_KEY`; the optional Tavily key can use `TAVILY_API_KEY`. The app never logs either. The separate Contree credentials are consumed by `contree-mcp` according to its Nebius configuration.

The first approved repair looks for the reusable `agentsquad/maintenance/dotnet-sdk:8.0` Sandbox image. Only when it is absent does it import the fixed public .NET 8 SDK image, explicitly acknowledging that anonymous public-registry access may be rate-limited; later runs reuse the tagged image. Before sync, it stages only the fixture's `.cs` and `.csproj` source files, never generated `bin`/`obj` output, and attaches them at fixed Linux container paths. These are fixed infrastructure steps, not agent-selected images or arbitrary registry pulls.

## Manually test a repaired snapshot

After a run reaches **Remediated**, Step 3 offers two optional manual checks: **Health check** and **Read work item 42**. Each button starts the repaired snapshot in a fresh disposable Nebius Sandbox, calls only the fixture's private `127.0.0.1` HTTP endpoint, and records the bounded response beside the original repair evidence.

The console accepts no custom command, URL, port, request body, or code change. One Sandbox operation may run at a time, each repair keeps at most ten recorded manual checks, and a failed manual check remains separate from the original remediation result. Nothing is exposed publicly and the host workspace, Git, pull requests, and deployments remain untouched.

In the Development launch profile, the console and maintenance API are open for local iteration. In Production, the whole console (except `/health`) first requires `AgentSquad__Web__OperatorUsername` and a unique 20+ character `AgentSquad__Web__OperatorPassword` on a one-time sign-in page; the server then issues an eight-hour, secure, HTTP-only session cookie. Every `/maintenance/*` endpoint independently requires a separate 32+ character `AgentSquad__Web__AccessToken` entered into the console. Neither value is a Nebius credential or an approval mechanism; this two-control boundary prevents an unknown visitor to the public URL from viewing the console or starting a Sandbox run.

## Deploy on Nebius

AgentSquad is ready to run as a Docker image on a CPU **Nebius Container VM**. The image runs as a non-root user, exposes port `8080`, performs a Docker health check, and installs the pinned ConTree MCP runtime. Its image build runs the solution tests before publishing the host.

See the operator-ready [Nebius deployment guide](deploy/nebius/README.md) for the Container VM steps, exact Docker arguments, credential boundaries, smoke test, and the short list of values to collect. Start with [runtime.env.example](deploy/nebius/runtime.env.example); it contains placeholders only and must never be populated or committed.

## Deploy on a VPS

The same container runs on a regular Linux VPS with Docker Compose, with Nebius Token Factory and Sandboxes retained as the external AI runtime. The deployment binds the app to loopback only and lets the existing HTTPS proxy own the public endpoint. See the [VPS deployment guide](docs/VPS_DEPLOYMENT.md) for the compose definition, OpenLiteSpeed templates, protected runtime configuration, verification, and optional local-only Portainer GUI.

## Verify without cloud credentials

```powershell
dotnet build AgentSquad.sln
dotnet vstest .\tests\AgentSquad.Tests.Unit\bin\Debug\net10.0\AgentSquad.Tests.Unit.dll --TestAdapterPath:.\tests\AgentSquad.Tests.Unit\bin\Debug\net10.0
dotnet build samples/maintenance-lab/scenarios/cache-repair/tests/MaintenanceLab.Tests.csproj
dotnet vstest .\samples\maintenance-lab\scenarios\cache-repair\tests\bin\Debug\net8.0\MaintenanceLab.Tests.dll --TestAdapterPath:.\samples\maintenance-lab\scenarios\cache-repair\tests\bin\Debug\net8.0
```

The `cache-repair` scenario intentionally produces NuGet warning `NU1903` before a repair; that is the vulnerability the Sandbox flow must remove from its staged copy. See the [Maintenance Lab](samples/maintenance-lab/README.md) for the three controlled scenarios.

For a submission-ready recording and the remaining Devpost checks, see the [video script](docs/SUBMISSION_VIDEO.md) and [submission checklist](docs/SUBMISSION_CHECKLIST.md).

## API

- `GET /health`
- `GET /maintenance/preflight`
- `GET /maintenance/scenarios`
- `GET /maintenance/manual-checks` — the fixed, loopback-only manual-check catalog
- `POST /maintenance/runs?scenario=<id>` — returns `202`; only one run may be active (`409` otherwise)
- `GET /maintenance/runs`
- `GET /maintenance/runs/{id}`
- `POST /maintenance/runs/{id}/manual-checks/{checkId}` — starts one allowed check against a remediated snapshot (`202`)

All `/maintenance/*` endpoints require the `X-AgentSquad-Access-Token` header in Production. `GET /health` stays unauthenticated so Nebius and external uptime checks can establish that the container is alive.

## Three-minute demo

Use the [timed recording script](docs/SUBMISSION_VIDEO.md). It shows one successful live repair with Tavily supplementary evidence and the two fail-closed paths, ending with `Remediated — Sandbox-only remediation verified`. No host repository or deployment changes.

## Project layout

```text
src/AgentSquad.Host/                  web console and HTTP API
src/AgentSquad.Agents/                coordinator, policy, Sentinel, and scheduler
src/AgentSquad.Plugin.Tools.NebiusSandbox/  constrained Contree MCP adapter
samples/maintenance-lab/              source-owned success and fail-closed .NET 8 demo scenarios
tests/AgentSquad.Tests.Unit/          policy and run-state tests
```

MIT License. See [LICENSE](LICENSE).
