# VPS deployment

AgentSquad can run on a regular Linux VPS; Nebius remains the inference and Sandbox provider. This is a low-cost alternative to a dedicated Nebius Container VM.

The production deployment uses Docker Compose with the application bound only to `127.0.0.1:8090`. OpenLiteSpeed owns the public HTTPS endpoint at `https://agentsquad.connect-the-dots.biz` and proxies requests to that loopback port. Do not expose port 8080 publicly.

## Runtime configuration

Create `/etc/agentsquad/agentsquad.env` on the VPS with mode `0600`. It must contain a unique 32+ character `AgentSquad__Web__AccessToken`. Add `NEBIUS_TOKEN_FACTORY_API_KEY`, `CONTREE_TOKEN`, and `CONTREE_PROJECT` before enabling maintenance. `TAVILY_API_KEY` is optional.

Keep `AgentSquad__Maintenance__Enabled=false` until the separate ConTree credentials have been tested. The unauthenticated `GET /health` endpoint stays available for the reverse proxy health check; all `/maintenance/*` endpoints require the access token in production.

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

Open `https://localhost:9443`, complete Portainer's initial administrator setup, choose the detected local environment, then manage the `agentsquad` compose project from its Containers and Stacks views. Do not publish Portainer or the Docker socket on a public port.
