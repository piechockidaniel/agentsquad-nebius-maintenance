# AgentSquad release and Devpost plan

## Readiness decision

**Do not submit yet.** The focused maintenance experience is live and its Sandbox path has been verified, but the public source, deployed image, documentation, and recorded video must describe the same immutable version. The target is the **Coding and Agentic Engineering** track.

Local evidence re-checked on 2026-09-12:

 - `dotnet test tests\AgentSquad.Tests.Unit\AgentSquad.Tests.Unit.csproj --no-restore` passed **28 / 28** tests after the persistent-session change.
 - `dotnet build .\src\AgentSquad.Host\AgentSquad.Host.csproj --no-restore -m:1` completed with **0 warnings and 0 errors**.
 - The deployable Host now uses the approved light, card-based console layout. Desktop and mobile rendering were checked locally; unavailable local integrations are shown as unavailable rather than as simulated successful preflight.
 - The generic `dotnet test AgentSquad.sln --no-restore` command currently discovers zero tests. Use the documented `dotnet vstest` command for the release gate unless the test-project adapter configuration is separately corrected.
 - `public-origin/main` currently matches the committed deployed source, but the persistent-session change is not yet committed, pushed, or deployed.
 - The VPS public `/health` endpoint is verified. Token Factory Sandboxes list the reusable AgentSquad .NET image, and the latest recorded Sandbox runs completed successfully. The Contree configuration must retain the Token Factory `aiproject-` ID and the corresponding Token Factory key.

## Deployment sequence

1. **Freeze the exact source.** The approved light, card-based layout is in `src/AgentSquad.Host/wwwroot/index.html`; the standalone prototype must not be mistaken for the release UI. Review the `feature/ui-console` diff, exclude the unrelated `Dockerfile.original`, run the local test command above, commit the intended files, tag the commit, and push it to `public-origin/main`. Confirm that the public repository displays the MIT license and the final README.
2. **Build an immutable VPS image.** From that commit, build `src/AgentSquad.Host/Dockerfile` on the VPS with the commit SHA as the image tag, run its test stage before release, and record its Docker image ID. The commit-derived image tag and image ID—not a mutable `latest` tag—are the deployment and recording target.
3. **Deploy through the existing VPS proxy.** Follow [the VPS deployment guide](VPS_DEPLOYMENT.md). Keep the app loopback-bound, preserve the protected session volume, and let OpenLiteSpeed own the public HTTPS endpoint.
4. **Configure runtime values privately.** Keep `NEBIUS_TOKEN_FACTORY_API_KEY`, `CONTREE_TOKEN`, `CONTREE_PROJECT`, any optional `TAVILY_API_KEY`, the console operator credentials, and `AgentSquad__Web__AccessToken` out of source, logs, screenshots, browser history, and Devpost. For Sandboxes, `CONTREE_TOKEN` is the same Token Factory key and `CONTREE_PROJECT` is the Token Factory `aiproject-` ID.
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

 - The persistent-session change still needs a reviewed commit, public push, and matching VPS image deployment.
- No commit-derived VPS image tag and image ID have been captured for the final source.
- The live preflight and completed Sandbox evidence need to be captured on-screen from that final deployed image for the video.
- The YouTube URL will be available after the recording is published.
- Devpost submission must wait until all of the above are checked and a final action-time review is complete.
