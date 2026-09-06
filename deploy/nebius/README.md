# Nebius Container VM deployment

This folder prepares the focused AgentSquad demo for a long-running **Nebius Compute → Container VMs** deployment. It does not contain a credential, cloud-project ID, or an automated cloud deployment: those belong to the Nebius project owner.

The app is intentionally a CPU service. NVIDIA inference is requested from Nebius Token Factory, and repair work runs in Token Factory Sandboxes; the web host itself does not need a GPU.

## 1. Build a release image

From the repository root, choose an immutable version tag and publish it to a registry available to the selected Nebius project.

```powershell
docker build --file src/AgentSquad.Host/Dockerfile --tag <registry>/agentsquad:<version> .
docker push <registry>/agentsquad:<version>
```

The image build runs the solution's unit tests before publishing the host. The image runs as the non-root `agentsquad` user, exposes port `8080`, contains the pinned ConTree MCP runtime, and includes a `/health` Docker health check.

For a Nebius Container Registry image in the same project, use its immutable digest in place of `<registry>/agentsquad:<version>` when you create the VM. For a private external registry, arrange pull credentials with the Nebius project owner; do not bake them into the image.

## 2. Collect and enter runtime configuration

Start with [runtime.env.example](runtime.env.example), but enter the values in the Nebius Container VM configuration rather than committing a populated file. `-e NAME=value` Docker run arguments are accepted by Docker, but keep the values out of screenshots, shell history, source files, and container logs.

Required values:

| Setting | Obtain from | Purpose |
| --- | --- | --- |
| `NEBIUS_TOKEN_FACTORY_API_KEY` | Nebius Token Factory project owner | Lets the app list available models and ask the configured NVIDIA model for the evidence-based risk summary. |
| `CONTREE_TOKEN` | The Token Factory Sandboxes / ConTree access flow | Authenticates the constrained MCP process to Sandboxes. |
| `CONTREE_PROJECT` | Nebius project details | Scopes the sandbox work to the correct Nebius project. |
| `AgentSquad__Web__AccessToken` | Generate a new random 32+ character value | Protects `/maintenance/*` on the public demo URL. It is unrelated to Nebius credentials. |
| `AgentSquad__Maintenance__Enabled=true` | Operator choice after all checks | Enables manual and weekday scheduled maintenance runs. |

Keep the default `CONTREE_URL` unless Nebius provides a different endpoint. ConTree resolves credentials as command-line arguments, then environment variables, then an optional profile; this deployment uses only runtime environment variables. Do not configure `CONTREE_PROFILE` as well as `CONTREE_TOKEN`/`CONTREE_PROJECT` unless the project owner deliberately uses a managed profile.

Useful non-secret overrides, normally left at their shipped defaults:

| Setting | Default | When to change it |
| --- | --- | --- |
| `AgentSquad__Maintenance__ModelId` | `nvidia/nemotron-3-super-120b-a12b` | Only when the Nebius project owner confirms a different NVIDIA model that is enabled for the key. |
| `AgentSquad__Maintenance__ScheduleCron` | `30 7 * * 1-5` | To change the weekday maintenance time; the cron expression is evaluated in UTC. |
| `AgentSquad__Maintenance__HistoryLimit` | `50` | To retain fewer or more in-memory evidence records (1–200). |
| `NebiusSandbox__ImageRegistryUrl` | `docker://mcr.microsoft.com/dotnet/sdk:8.0` | Only if the controlled fixture needs a different pre-approved sandbox image. |

Do not set `NebiusSandbox__Command` in the deployment; the image already installs the pinned `contree-mcp` command. Do not provide GitHub, Git, PR, deployment, Azure DevOps, or Teams credentials—this version does not use them.

## 3. Create the Container VM

In the Nebius console:

1. Go to **Compute → Container VMs → Create container VM** and select the correct Nebius project.
2. Choose **Custom image**, enter the published immutable image reference, and choose a regular CPU configuration. A small general-purpose CPU preset is sufficient for the web host; the model and sandbox compute live in Token Factory.
3. Set Docker run arguments to the exact contents of [docker-run-arguments.txt](docker-run-arguments.txt): `--restart=always -p 8080:8080`.
4. Add the runtime variables from the preceding section. Do not place the actual values in the image, repository, or a public issue.
5. Assign a public IPv4 address for the hackathon demo. Add a non-reserved SSH username and its public key for emergency log inspection. Nebius reserves `root` and `admin` for internal use.
6. Create the VM and wait for its web UI link to become available.

Nebius supports custom public-registry images and provides a public web UI link when the Container VM has a public IP. The `-p 8080:8080` mapping makes AgentSquad's listening port reachable through that link. Use an immutable image digest rather than a mutable tag when recording the submission demo.

## 4. Verify before the demo

1. Open the Container VM's **Go to Web UI** link.
2. Confirm `GET /health` responds with `200`.
3. Paste `AgentSquad__Web__AccessToken` into the console's **Console access token** field. The browser holds it only for the current session.
4. Select **Check Nebius preflight**. It must show both the NVIDIA model and ConTree sandbox tools as ready.
5. Run one repair. Capture the advisory, manifest-only diff, test output, clean scan, and sandbox snapshot ID.

If a preflight or repair fails, inspect the Container VM's Docker logs over SSH. Never paste the output into a public issue or recording without checking that it contains no secret.

## Values the operator must have before starting

- Nebius organization/project selection and the intended region.
- Permission to create a Container VM with a public IP, plus a CPU preset/quota.
- The final immutable container image reference and, if necessary, registry pull credentials.
- A public SSH key and a non-`root`/non-`admin` username for emergency inspection.
- A Token Factory API key with access to the selected NVIDIA model.
- Token Factory Sandboxes / ConTree access, including a `CONTREE_TOKEN` and `CONTREE_PROJECT`.
- A newly generated AgentSquad web access token (32+ characters).

No repository write, pull request, merge, or production deployment credential is required or accepted by the AgentSquad maintenance loop.
