# VPS deployment

AgentSquad can run on a regular Linux VPS; Nebius remains the inference and Sandbox provider. This is a low-cost alternative to a dedicated Nebius Container VM.

The production deployment uses Docker Compose with the application bound only to `127.0.0.1:8090`. OpenLiteSpeed owns the public HTTPS endpoint at `https://agentsquad.connect-the-dots.biz` and proxies requests to that loopback port. Do not expose port 8080 publicly.

## Runtime configuration

Create `/etc/agentsquad/agentsquad.env` on the VPS with mode `0600`. It must contain a unique operator username, a unique 20+ character `AgentSquad__Web__OperatorPassword`, and a separate 32+ character `AgentSquad__Web__AccessToken`. The operator credentials protect the public console with Basic authentication; the token independently protects every maintenance API request. Add `NEBIUS_TOKEN_FACTORY_API_KEY`, `CONTREE_TOKEN`, and `CONTREE_PROJECT` before enabling maintenance. `TAVILY_API_KEY` is optional.

Keep `AgentSquad__Maintenance__Enabled=false` until the separate ConTree credentials have been tested. The unauthenticated `GET /health` endpoint stays available for the reverse proxy health check; all other production routes require the operator credentials, and all `/maintenance/*` endpoints also require the access token.

## Release and verification

Build from the public source at an immutable Git commit, then run:

```bash
docker compose -f deploy/vps/docker-compose.yml up -d --build
curl --fail http://127.0.0.1:8090/health
```

The OpenLiteSpeed templates in `deploy/vps/openlitespeed/` first serve the ACME HTTP challenge and then enable the certificate-specific TLS block. Retain an OpenLiteSpeed configuration backup before registering the vhost and listener mappings. Confirm the deployed certificate with SNI, the public `/health` endpoint, the static console, and the authenticated preflight.

## GUI management

Portainer CE may run bound to `127.0.0.1:9443` with its data in a named Docker volume. Access it only through an SSH tunnel:

```powershell
ssh -L 9443:127.0.0.1:9443 root@connect-the-dots.biz
```

Open `https://localhost:9443`, complete Portainer's initial administrator setup, and choose the detected local environment. The running `agentsquad-host-1` container is available from **Containers** for logs, status checks, and start/stop/restart actions. Portainer does not automatically import Compose projects initially started from the command line into its Stacks view; keep using the versioned compose file for releases unless you deliberately migrate management to a Portainer stack. Do not publish Portainer or the Docker socket on a public port.
