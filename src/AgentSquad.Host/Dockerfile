FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AgentSquad.sln ./
COPY src/AgentSquad.Agents/AgentSquad.Agents.csproj src/AgentSquad.Agents/
COPY src/AgentSquad.Host/AgentSquad.Host.csproj src/AgentSquad.Host/
COPY src/AgentSquad.Plugin.Tools.NebiusSandbox/AgentSquad.NebiusSandbox.csproj src/AgentSquad.Plugin.Tools.NebiusSandbox/
COPY tests/AgentSquad.Tests.Unit/AgentSquad.Tests.Unit.csproj tests/AgentSquad.Tests.Unit/
RUN dotnet restore AgentSquad.sln
COPY src/AgentSquad.Agents/Configuration/AgentSquadOptions.cs src/AgentSquad.Agents/Configuration/
COPY src/AgentSquad.Agents/Maintenance/ src/AgentSquad.Agents/Maintenance/
COPY src/AgentSquad.Host/Program.cs src/AgentSquad.Host/
COPY src/AgentSquad.Host/appsettings.json src/AgentSquad.Host/
COPY src/AgentSquad.Host/wwwroot/ src/AgentSquad.Host/wwwroot/
COPY src/AgentSquad.Plugin.Tools.NebiusSandbox/AgentSquad.NebiusSandbox.csproj src/AgentSquad.Plugin.Tools.NebiusSandbox/
COPY src/AgentSquad.Plugin.Tools.NebiusSandbox/ContreeMaintenanceSandbox.cs src/AgentSquad.Plugin.Tools.NebiusSandbox/
COPY tests/AgentSquad.Tests.Unit/GlobalUsings.cs tests/AgentSquad.Tests.Unit/
COPY tests/AgentSquad.Tests.Unit/Maintenance/ tests/AgentSquad.Tests.Unit/Maintenance/
COPY samples/maintenance-fixture/ samples/maintenance-fixture/

FROM build AS test
RUN dotnet test --solution AgentSquad.sln -c Release --no-restore

FROM test AS publish
RUN dotnet publish src/AgentSquad.Host/AgentSquad.Host.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl python3 python3-venv \
    && python3 -m venv /opt/contree \
    && /opt/contree/bin/pip install --no-cache-dir contree-mcp==0.2.0 \
    && groupadd --gid 10001 agentsquad \
    && useradd --uid 10001 --gid agentsquad --create-home --shell /usr/sbin/nologin agentsquad \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    CONTREE_HOME=/home/agentsquad/.config/contree \
    DOTNET_EnableDiagnostics=0 \
    PATH=/opt/contree/bin:${PATH}
EXPOSE 8080
COPY --from=publish --chown=agentsquad:agentsquad /app/publish .
USER agentsquad
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD curl --fail --silent http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "AgentSquad.Host.dll"]
