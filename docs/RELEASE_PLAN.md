# AgentSquad release and Devpost plan

## Readiness decision

**Ready for final recording and Devpost drafting.** The focused maintenance experience, public source, immutable VPS image, protected sign-in session store, and deployment documentation are now aligned. Do not press Devpost's irreversible **Submit** action until the recorded video and every final field have been reviewed. The target is the **Coding and Agentic Engineering** track.

Local evidence re-checked on 2026-09-12:

 - `dotnet test tests\AgentSquad.Tests.Unit\AgentSquad.Tests.Unit.csproj --no-restore` passed **28 / 28** tests after the persistent-session change.
 - `dotnet build .\src\AgentSquad.Host\AgentSquad.Host.csproj --no-restore -m:1` completed with **0 warnings and 0 errors**.
 - The deployable Host now uses the approved light, card-based console layout. Desktop and mobile rendering were checked locally; unavailable local integrations are shown as unavailable rather than as simulated successful preflight.
 - The generic `dotnet test AgentSquad.sln --no-restore` command currently discovers zero tests. Use the documented `dotnet vstest` command for the release gate unless the test-project adapter configuration is separately corrected.
 - The container-image test stage re-ran the Release unit suite and passed **28 / 28** tests before the VPS switch.
 - `public-origin/main` matches the released source. Each VPS deployment uses a commit-derived image tag rather than `latest`, and the public `/health` endpoint is verified after the switch.
 - The VPS keeps the SHA-256 session store in its protected volume. Token Factory Sandboxes list the reusable AgentSquad .NET image, and the latest recorded Sandbox runs completed successfully. The Contree configuration must retain the Token Factory `aiproject-` ID and the corresponding Token Factory key.

## Deployment sequence

1. **Freeze the exact source.** For a future change, review the `feature/ui-console` diff, exclude unrelated working-tree files such as `Dockerfile.original`, run the local test command above, commit the intended files, tag the commit, and push it to `public-origin/main`. Confirm that the public repository displays the MIT license and the final README.
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

## Remaining release gates

- Capture the live preflight and completed Sandbox evidence on-screen from the final deployed image for the video.
- Publish the public YouTube video and add its URL to the Devpost draft.
- Create dedicated, revocable review credentials and give judges only the console username/password and separate console access token — never a Nebius, Contree, Tavily, or Token Factory credential.
- Re-open every saved Devpost field, confirm the public demo and repository work in a fresh browser session, then make the final action-time review before submission.
