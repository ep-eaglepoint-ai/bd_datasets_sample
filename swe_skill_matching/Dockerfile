FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip \
    NUGET_FALLBACK_PACKAGES=

WORKDIR /src

# Cache restores
COPY BD-RL-009-swe_skill_matching.sln ./
COPY BD-RL-009-swe_skill_matching.csproj ./
COPY repository_before/repository_before.csproj repository_before/
COPY repository_after/repository_after.csproj repository_after/
COPY evaluation/evaluation.csproj evaluation/
COPY tests/tests.csproj tests/

RUN dotnet restore BD-RL-009-swe_skill_matching.sln /p:DisableImplicitNuGetFallbackFolder=true

# Build the whole solution
COPY . .
RUN dotnet build BD-RL-009-swe_skill_matching.sln -c Release --no-restore /p:DisableImplicitNuGetFallbackFolder=true

FROM build AS tests
RUN dotnet test tests/tests.csproj -c Release --no-build --logger "trx;LogFileName=test-results.trx" /p:DisableImplicitNuGetFallbackFolder=true

FROM build AS publish
RUN dotnet publish repository_after/repository_after.csproj -c Release -o /app/repository_after --no-build /p:DisableImplicitNuGetFallbackFolder=true

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=publish /app/repository_after ./repository_after

ENTRYPOINT ["dotnet", "repository_after/repository_after.dll"]
