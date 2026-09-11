# VPS deployment

AgentSquad can run on a regular Linux VPS; Nebius remains the inference and Sandbox provider. This is a low-cost alternative to a dedicated Nebius Container VM.

The production deployment uses Docker Compose with the application bound only to `127.0.0.1:8090`. OpenLiteSpeed owns the public HTTPS endpoint at `https://agentsquad.connect-the-dots.biz` and proxies requests to that loopback port. Do not expose port 8080 publicly.

## Runtime configuration

Create `/etc/agentsquad/agentsquad.env` on the VPS with mode `0600`. It must contain a unique operator username, a unique 20+ character `AgentSquad__Web__OperatorPassword`, and a separate 32+ character `AgentSquad__Web__AccessToken`. The operator credentials protect the public console with a one-time sign-in and secure session cookie; the token independently protects every maintenance API request.

For the verified Token Factory Sandbox setup, set `NEBIUS_TOKEN_FACTORY_API_KEY` to the AgentSquad Token Factory API key, set `CONTREE_TOKEN` to that same key, and set `CONTREE_PROJECT` to the Token Factory project ID beginning `aiproject-`. Do **not** use the Nebius Console URL's `project-` identifier for `CONTREE_PROJECT`; it cannot list this Token Factory Sandbox inventory. `TAVILY_API_KEY` is optional. Values do not need quotes when they contain no spaces; never add them to source, browser storage, logs, or screenshots.

Keep `AgentSquad__Maintenance__Enabled=false` until the separate ConTree credentials have been tested. The unauthenticated `GET /health` endpoint stays available for the reverse proxy health check; all other production routes require the operator credentials, and all `/maintenance/*` endpoints also require the access token. Do not put either operator credential in browser storage. The Compose definition keeps only SHA-256 hashes of eight-hour opaque sign-in sessions in `/srv/agentsquad/data/sessions`, allowing a normal container recreation without a new sign-in.

## Release and verification

Build from the public source at an immutable Git commit. Before the first release, create the server-only session directory with the same non-root UID/GID used by the container. Build a tag derived from the exact commit, record its Docker image ID, add `AGENTSQUAD_IMAGE=agentsquad:<commit-sha>` to the protected environment file, then run:

```bash
cd /srv/agentsquad/current
release_sha="$(git rev-parse --short=12 HEAD)"
sudo install -d -m 0700 -o 10001 -g 10001 /srv/agentsquad/data/sessions
docker build -t "agentsquad:${release_sha}" -f src/AgentSquad.Host/Dockerfile .
# Set AGENTSQUAD_IMAGE=agentsquad:${release_sha} in /etc/agentsquad/agentsquad.env.
docker compose --env-file /etc/agentsquad/agentsquad.env -f deploy/vps/docker-compose.yml up -d --no-build --force-recreate
docker image inspect "agentsquad:${release_sha}" --format '{{.Id}}'
curl --fail http://127.0.0.1:8090/health
```

The session file contains no password, provider key, API access token, or raw browser cookie. Deleting it deliberately signs every operator out; changing the operator password should be followed by deleting this file and recreating the container. The successful release gate is public `/health`, authenticated preflight, and the free Contree `list_images` check returning the reusable `agentsquad/maintenance/dotnet-sdk:8.0` image before a recording run.

The OpenLiteSpeed templates in `deploy/vps/openlitespeed/` first serve the ACME HTTP challenge and then enable the certificate-specific TLS block. Retain an OpenLiteSpeed configuration backup before registering the vhost and listener mappings. Confirm the deployed certificate with SNI, the public `/health` endpoint, the static console, and the authenticated preflight.

## GUI management

Portainer CE may run bound to `127.0.0.1:9443` with its data in a named Docker volume. Access it only through an SSH tunnel:

```powershell
ssh -L 9443:127.0.0.1:9443 root@connect-the-dots.biz
```

Open `https://localhost:9443`, complete Portainer's initial administrator setup, and choose the detected local environment. The running `agentsquad-host-1` container is available from **Containers** for logs, status checks, and start/stop/restart actions. Portainer does not automatically import Compose projects initially started from the command line into its Stacks view; keep using the versioned compose file for releases unless you deliberately migrate management to a Portainer stack. Do not publish Portainer or the Docker socket on a public port.

## Telemetry and log retention

AgentSquad emits structured JSON telemetry to standard output. Events record maintenance run IDs, selected scenarios, safe terminal outcomes, preflight availability, and Sandbox tool names; they deliberately omit API keys, access tokens, request bodies, and successful provider output. The Compose service uses Docker's local log driver with a seven-file, 10 MB-per-file rotation limit (at most 70 MB). Inspect retained events in **Portainer → Containers → agentsquad-host-1 → Logs**, or over SSH with `docker logs agentsquad-host-1`. The Docker-managed log store persists while the container is recreated, subject to Docker's normal cleanup and the configured rotation limit.
