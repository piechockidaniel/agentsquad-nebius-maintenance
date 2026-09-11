# AgentSquad submission checklist

Target track: **Coding and Agentic Engineering**.

AgentSquad has live evidence for its core claim: the `cache-repair` scenario detected `GHSA-qj66-m88j-hmgj`, staged the sole allowed `8.0.0` to `8.0.1` change in a Nebius Sandbox, passed the test suite, and produced a clean post-patch scan. The two additional scenarios demonstrate policy blocking and baseline-test failure.

## Ready in the repository

- Public-source-friendly focused codebase with an MIT [license](../LICENSE).
- Setup, local verification, credential boundaries, Nebius deployment instructions, and the deterministic policy in the [README](../README.md).
- A controlled .NET 8 Maintenance Lab with a successful repair, an unapproved-scope block, and a failing-baseline stop condition.
- Explicit disclosure of where Nebius Token Factory, the NVIDIA Nemotron model, Token Factory Sandboxes, Contree MCP, and Tavily Search are used.
- A 2:30 recording plan in [SUBMISSION_VIDEO.md](SUBMISSION_VIDEO.md).

## Complete before pressing Submit

- [ ] The GitHub repository is public, points to the final code, and displays the MIT license in its About section.
- [ ] The deployed VPS URL is reachable and `/health` returns `200`.
- [ ] The deployed image is an immutable tag or digest that matches the public repository revision.
- [ ] A dedicated, revocable web-console token is available for judges in the Devpost testing instructions. Do not publish Nebius, Contree, or Token Factory credentials.
- [ ] Record and publicly upload the English, narrated YouTube video. Keep it under three minutes and show the working app, not only slides or source code.
- [ ] Choose **Coding and Agentic Engineering** on Devpost.
- [ ] In the project description, state that Tavily retains bounded supplementary security sources; the NVIDIA model produces the grounded risk explanation; deterministic policy chooses the only permitted package/version; Sandbox tools stage, test, and rescan the change.
- [ ] State plainly that v1 never changes the host repository, Git history, pull requests, or deployments.
- [ ] Explain the significant Submission Period update if the entry is based on pre-existing AgentSquad work: “During the Submission Period, AgentSquad gained the focused Nebius Token Factory/NVIDIA maintenance flow, bounded Tavily security research, constrained Contree Sandbox adapter, controlled repair lab, evidence console, and deployment documentation.”
- [ ] Provide concise feedback on the Nebius and NVIDIA tooling in the Devpost feedback field.

## Reviewer testing instructions

Use this as the basis for the Devpost testing field, replacing the bracketed values only at submission time:

> Open [deployed AgentSquad URL] with the supplied short-lived review username and password. Confirm `/health` returns `200`. In the console, enter the separate dedicated review access token, select **Known dependency repair**, and click **Run sandbox repair**. The expected result is `Remediated`, with the exact manifest-only `Microsoft.Extensions.Caching.Memory 8.0.0 → 8.0.1` diff, passing tests, clean vulnerability rescan, and a Sandbox snapshot ID. The `Unapproved second dependency` scenario must end `Blocked`; `Baseline test failure` must end `Failed` before a patch is staged. The review credentials grant access only to AgentSquad's web console. They are not Nebius, Contree, or Token Factory credentials.

Use unique, short-lived review credentials and rotate both the operator password and access token after the judging period. Keep the application publicly reachable through the end of judging, as required by the rules.
