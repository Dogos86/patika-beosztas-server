FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build

WORKDIR /source

COPY global.json ./
COPY eng/Common.Build.props eng/Common.Build.props
COPY src/Directory.Build.props src/Directory.Build.props
COPY src/PatikaBeosztas.Api/PatikaBeosztas.Api.csproj src/PatikaBeosztas.Api/
COPY src/PatikaBeosztas.Application/PatikaBeosztas.Application.csproj src/PatikaBeosztas.Application/
COPY src/PatikaBeosztas.Contracts/PatikaBeosztas.Contracts.csproj src/PatikaBeosztas.Contracts/
COPY src/PatikaBeosztas.Domain/PatikaBeosztas.Domain.csproj src/PatikaBeosztas.Domain/
COPY src/PatikaBeosztas.Infrastructure/PatikaBeosztas.Infrastructure.csproj src/PatikaBeosztas.Infrastructure/

RUN dotnet restore src/PatikaBeosztas.Api/PatikaBeosztas.Api.csproj

COPY src src

RUN dotnet publish src/PatikaBeosztas.Api/PatikaBeosztas.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates gosu libgssapi-krb5-2 tzdata \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 patika \
    && useradd --uid 10001 --gid patika --no-create-home --shell /usr/sbin/nologin patika

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    TZ=Europe/Budapest

WORKDIR /app

COPY --from=build /app/publish ./
COPY backend/entrypoint.sh /app/entrypoint.sh
COPY scripts/railway-migrate.sh /app/scripts/railway-migrate.sh

RUN mkdir --parents /app/keys \
    && chown --recursive patika:patika /app \
    && chmod 0555 /app/entrypoint.sh /app/scripts/railway-migrate.sh

EXPOSE 8080

ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["dotnet", "/app/PatikaBeosztas.Api.dll"]
