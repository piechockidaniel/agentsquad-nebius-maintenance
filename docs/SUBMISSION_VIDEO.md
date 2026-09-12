# AgentSquad submission video — 2 minutes 45 seconds

Record a fresh run in the deployed web console. The recorded application must be the same version and environment that a judge can access. Keep browser tabs, terminals, environment variables, access tokens, IAM/project details, and cloud-billing information out of frame. Use no background music unless you have the rights to it.

## Before recording

1. Deploy the immutable container image and open its public Web UI link.
2. Confirm `GET /health` returns `200`; opening the console starts its automatic preflight and should report the NVIDIA model and Contree Sandbox tools as ready.
3. Ensure the reusable `agentsquad/maintenance/dotnet-sdk:8.0` image exists. That avoids recording a first-time public-registry import delay.
4. Start with an empty or uncluttered run history. The three controlled scenarios are all that need to be shown.
5. Use dedicated, revocable operator credentials and a separate web-console access token for the recording and judge instructions. Do not show any of their values.

## Spoken script and screen actions

| Time | Screen action | Suggested narration |
| --- | --- | --- |
| 0:00–0:15 | Open the AgentSquad console. | “AgentSquad is a safety-first maintenance team for a common production risk: a vulnerable dependency. It repairs only a pre-approved change, only inside a disposable Nebius Sandbox.” |
| 0:15–0:30 | Keep the automatic preflight visible: its grey badges turn orange while probing, then green when the NVIDIA model and Sandbox connect. Keep the model name, sandbox tool list, and Tavily state visible. | “Opening the console automatically checks its critical dependencies. It makes at most five progressively delayed probes, then pauses briefly instead of retrying forever. Nebius Token Factory provides the NVIDIA Nemotron model for an evidence-grounded risk explanation. Contree connects the app to Nebius Token Factory Sandboxes for the write, test, and scan work. Tavily adds bounded public security-source research.” |
| 0:30–0:40 | When the paths open, click **Run Path 01** on **Repair one known dependency vulnerability**. | “The coordinator is deterministic. It permits exactly one direct package: Microsoft.Extensions.Caching.Memory from 8.0.0 to 8.0.1, for this confirmed advisory.” |
| 0:40–1:25 | Let the real run complete. Open its evidence and scroll deliberately through Tavily sources, the advisory, manifest diff, baseline scan, and both test results. | “Dependency Sentinel confirms the package and GitHub advisory, then uses Tavily to retain a few trusted security sources. The NVIDIA model summarizes the risk, but neither service can choose a version or command. Patch & Verify stages the manifest-only diff in the Sandbox, restores, runs the tests, and rescans.” |
| 1:25–1:40 | Show **Remediated**, the clean post-patch scan, and the Sandbox snapshot ID. | “The patch passed the test suite and the rescan reports no vulnerable packages. This snapshot is Sandbox evidence only: AgentSquad did not change the host repository, Git, a pull request, or a deployment.” |
| 1:40–1:55 | Use **Try the repaired snapshot** to run **Health check**. Show the captured response. | “After remediation, an operator can manually exercise the built artifact. This starts the patched snapshot in a fresh disposable Sandbox and calls its private health endpoint—there is no public preview URL, arbitrary command, or host change.” |
| 1:55–2:10 | Select **Attempt an unapproved dependency change** and click **Run Path 02**. Show the policy step and final `Blocked` state. | “When scope expands to a second direct package, the policy blocks the run before any Sandbox mutation. The system fails closed instead of improvising a broader fix.” |
| 2:10–2:30 | Select **Start with a failing test baseline** and click **Run Path 03**. Show the baseline test failure and `Failed` result. | “If the application was already failing its baseline test, AgentSquad stops before it stages a patch. A dependency update must not mask unrelated breakage.” |
| 2:30–2:45 | Return to the successful run. Keep the clean scan, exact diff, and manual-check response on screen. | “AgentSquad turns one small, auditable maintenance decision into verified evidence using Nebius Token Factory, NVIDIA Nemotron, and isolated Sandboxes—while keeping code integration and release authority outside this v1 flow.” |

## Required proof to capture

- Preflight names `nvidia/nemotron-3-super-120b-a12b` and reports the Contree Sandbox connection.
- The completed run contains `tavily_security_research` with bounded sources from approved security domains.
- The successful run visibly contains `GHSA-qj66-m88j-hmgj`, the exact `8.0.0` to `8.0.1` manifest diff, a passing baseline test, a passing patched test, and a clean post-patch scan.
- Its final state is `Remediated` and includes a non-empty sandbox snapshot ID.
- The optional manual health check shows its captured private Sandbox response, without exposing a preview URL.
- One `Blocked` and one `Failed` scenario visibly stop at their stated safety boundary.

Do not use edited output, synthetic terminal text, or a pre-recorded run presented as live. Trimming waits and switching between completed console views is fine, provided the video accurately represents the deployed version.
