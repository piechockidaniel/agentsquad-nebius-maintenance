# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile.

# The base stage is also used by Visual Studio's fast mode, so install the
# Sandbox connector here rather than only in the final production stage.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl python3 python3-venv \
    && python3 -m venv /opt/contree \
    && /opt/contree/bin/pip install --no-cache-dir contree-mcp==0.2.0 mcp==1.9.4 \
    && /opt/contree/bin/contree-mcp --help > /dev/null \
    && groupadd --gid 10001 agentsquad \
    && useradd --uid 10001 --gid agentsquad --create-home --shell /usr/sbin/nologin agentsquad \
    && install -d --owner=agentsquad --group=agentsquad --mode=0700 /var/lib/agentsquad/sessions \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    CONTREE_HOME=/home/agentsquad/.config/contree \
    DOTNET_EnableDiagnostics=0 \
    PATH=/opt/contree/bin:${PATH}
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build and test the focused solution before producing the image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY global.json ./
COPY src/AgentSquad.Agents/AgentSquad.Agents.csproj src/AgentSquad.Agents/
COPY src/AgentSquad.Host/AgentSquad.Host.csproj src/AgentSquad.Host/
COPY src/AgentSquad.Plugin.Tools.NebiusSandbox/AgentSquad.NebiusSandbox.csproj src/AgentSquad.Plugin.Tools.NebiusSandbox/
COPY tests/AgentSquad.Tests.Unit/AgentSquad.Tests.Unit.csproj tests/AgentSquad.Tests.Unit/
RUN dotnet restore src/AgentSquad.Host/AgentSquad.Host.csproj \
    && dotnet restore tests/AgentSquad.Tests.Unit/AgentSquad.Tests.Unit.csproj
COPY src/AgentSquad.Agents/Configuration/AgentSquadOptions.cs src/AgentSquad.Agents/Configuration/
COPY src/AgentSquad.Agents/Configuration/ProductionWebAccess.cs src/AgentSquad.Agents/Configuration/
COPY src/AgentSquad.Agents/Configuration/OperatorSessionStore.cs src/AgentSquad.Agents/Configuration/
COPY src/AgentSquad.Agents/Maintenance/ src/AgentSquad.Agents/Maintenance/
COPY src/AgentSquad.Host/Program.cs src/AgentSquad.Host/
COPY src/AgentSquad.Host/OperatorSignInPage.cs src/AgentSquad.Host/
COPY src/AgentSquad.Host/appsettings.json src/AgentSquad.Host/
COPY src/AgentSquad.Host/wwwroot/ src/AgentSquad.Host/wwwroot/
COPY src/AgentSquad.Plugin.Tools.NebiusSandbox/ContreeMaintenanceSandbox.cs src/AgentSquad.Plugin.Tools.NebiusSandbox/
COPY tests/AgentSquad.Tests.Unit/GlobalUsings.cs tests/AgentSquad.Tests.Unit/
COPY tests/AgentSquad.Tests.Unit/Configuration/ tests/AgentSquad.Tests.Unit/Configuration/
COPY tests/AgentSquad.Tests.Unit/Maintenance/ tests/AgentSquad.Tests.Unit/Maintenance/
COPY samples/maintenance-lab/ samples/maintenance-lab/

FROM build AS test
ARG BUILD_CONFIGURATION=Release
RUN dotnet build tests/AgentSquad.Tests.Unit/AgentSquad.Tests.Unit.csproj -c $BUILD_CONFIGURATION --no-restore \
    && dotnet vstest tests/AgentSquad.Tests.Unit/bin/$BUILD_CONFIGURATION/net10.0/AgentSquad.Tests.Unit.dll \
        --TestAdapterPath:tests/AgentSquad.Tests.Unit/bin/$BUILD_CONFIGURATION/net10.0 \
        --logger:"console;verbosity=minimal"

FROM test AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish src/AgentSquad.Host/AgentSquad.Host.csproj -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=publish --chown=agentsquad:agentsquad /app/publish .
USER agentsquad
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD curl --fail --silent http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "AgentSquad.Host.dll"]
