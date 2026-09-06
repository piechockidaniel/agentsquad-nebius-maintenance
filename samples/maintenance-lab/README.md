# AgentSquad Maintenance Lab

This source-owned .NET 8 lab makes the hackathon demonstration repeatable without writing to a real product repository. Each scenario begins from its committed state and is copied into a disposable Nebius Token Factory Sandbox before AgentSquad does any work.

| Scenario | Expected outcome | Why it exists |
| --- | --- | --- |
| `cache-repair` | `Remediated` | The approved `Microsoft.Extensions.Caching.Memory` 8.0.0 to 8.0.1 remediation is staged, tested, and rescanned in the Sandbox. |
| `unapproved-package` | `Blocked` | An additional direct dependency violates the one-package policy. No Sandbox mutation is requested. |
| `baseline-test-failure` | `Failed` | A pre-existing test fails before patch staging. This proves AgentSquad does not mask unrelated breakage. |

The lab contains no exploit code. It uses the known advisory `GHSA-qj66-m88j-hmgj` only as deterministic dependency-maintenance evidence.

Run the successful scenario locally:

```powershell
dotnet test samples/maintenance-lab/scenarios/cache-repair/tests/MaintenanceLab.Tests.csproj
```
