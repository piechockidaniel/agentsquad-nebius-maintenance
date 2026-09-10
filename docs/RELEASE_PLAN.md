# AgentSquad release and Devpost plan

## Readiness decision

**Do not submit yet.** The focused maintenance experience, deployment runbook, and local tests are in place, but the public release and live proof must describe the same immutable version. The target is the **Coding and Agentic Engineering** track.

Local evidence re-checked on 2026-09-10:

 - `dotnet vstest .\tests\AgentSquad.Tests.Unit\bin\Debug\net10.0\AgentSquad.Tests.Unit.dll --TestAdapterPath:.\tests\AgentSquad.Tests.Unit\bin\Debug\net10.0` passed **14 / 14** tests.
 - `dotnet build .\src\AgentSquad.Host\AgentSquad.Host.csproj --no-restore -m:1` completed with **0 warnings and 0 errors**.
 - The deployable Host now uses the approved light, card-based console layout. Desktop and mobile rendering were checked locally; unavailable local integrations are shown as unavailable rather than as simulated successful preflight.
 - The generic `dotnet test AgentSquad.sln --no-restore` command currently discovers zero tests. Use the documented `dotnet vstest` command for the release gate unless the test-project adapter configuration is separately corrected.
 - The focused maintenance workflow and console changes must be reviewed, committed, and pushed to `public-origin/main` before the repository URL is submitted.
- A fresh Container VM deployment, Token Factory model probe, ConTree Sandbox preflight, and completed sandbox repair have **not** been re-verified in this release pass. Do not represent them as current live proof until the evidence is captured.

## Deployment sequence

1. **Freeze the exact source.** The approved light, card-based layout is in `src/AgentSquad.Host/wwwroot/index.html`; the standalone prototype must not be mistaken for the release UI. Review the `feature/ui-console` diff, run the local test command above, commit the intended files, and push only that commit to the public repository. Confirm that the public repository displays the MIT license and the final README.
2. **Build an immutable image.** From that commit, build `src/AgentSquad.Host/Dockerfile`, run its test stage, push a versioned tag, and record its registry digest. The digest—not a mutable `latest` tag—is the deployment and recording target.
3. **Create the Nebius Container VM.** Follow [deploy/nebius/README.md](../deploy/nebius/README.md). Use a CPU Container VM with the immutable image, `--restart=always -p 8080:8080`, a public web UI link, and a non-privileged SSH account with an SSH key only.
4. **Configure runtime values privately.** Enter the names and values from [runtime.env.example](../deploy/nebius/runtime.env.example) in the Container VM configuration. Keep `NEBIUS_TOKEN_FACTORY_API_KEY`, `CONTREE_TOKEN`, `CONTREE_PROJECT`, any optional `TAVILY_API_KEY`, the console operator credentials, and `AgentSquad__Web__AccessToken` out of source, logs, screenshots, browser history, and Devpost.
5. **Run the live release gate.** Confirm the public `/health` endpoint returns `200`, enter the dedicated operator credentials and web-console access token, and run preflight. It must identify the configured NVIDIA model and available Contree tools. Then run `cache-repair`, record its manifest-only diff, baseline and patched test output, clean scan, and sandbox snapshot ID. Run the blocked and failing-baseline paths as fail-closed evidence.
6. **Record and publish the video.** Follow [SUBMISSION_VIDEO.md](SUBMISSION_VIDEO.md). It is a narrated **2:45** script, leaving a 15-second limit buffer under Devpost's three-minute maximum. The video must show the deployed application and a fresh run, not synthetic or edited evidence.
7. **Prepare the Devpost draft.** Add the public repository URL, deployed demo URL, public YouTube link, Coding and Agentic Engineering track, project description, testing instructions from [SUBMISSION_CHECKLIST.md](SUBMISSION_CHECKLIST.md), significant submission-period update, and concise Nebius/NVIDIA feedback.
8. **Final review, then submit.** Re-open every Devpost field after saving, verify the URLs and selected track, confirm the public demo and repository work without an owner session, and obtain a final go-ahead immediately before the irreversible Submit action.

## Devpost positioning

**Title:** AgentSquad — Sandbox-Verified Dependency Repair

**One-line summary:** A safety-first coding agent that verifies one approved dependency repair in a disposable Nebius Sandbox, collects evidence, and leaves merge and deployment decisions to a human.

**Significant submission-period update:** During the Submission Period, AgentSquad gained the focused Nebius Token Factory/NVIDIA maintenance flow, bounded Tavily security research, constrained Contree Sandbox adapter, controlled repair lab, evidence console, and deployment documentation.

**Testing promise:** Judges receive a dedicated, revocable AgentSquad console token. It authorizes only the demo's bounded maintenance API; it is not a Nebius, ConTree, Git, or cloud credential. The test flow is the exact one in [SUBMISSION_CHECKLIST.md](SUBMISSION_CHECKLIST.md).

## Current blockers

 - No immutable release image digest has been captured for the final commit.
- No deployed public URL or current `/health` evidence exists for this release pass.
- The required live Token Factory and Sandbox evidence must be captured from the deployed build.
- The YouTube URL will be available after the recording is published.
- Devpost submission must wait until all of the above are checked and a final action-time review is complete.
