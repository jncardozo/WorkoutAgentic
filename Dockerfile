FROM node:22-bookworm-slim AS web-build
WORKDIR /web
COPY src/web/package.json src/web/package-lock.json ./
RUN npm install --global npm@11.19.0 && npm ci
COPY src/web/ ./
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /build
COPY src/api/WorkoutAgentic.Api/WorkoutAgentic.Api.csproj src/api/WorkoutAgentic.Api/
RUN dotnet restore src/api/WorkoutAgentic.Api/WorkoutAgentic.Api.csproj
COPY src/api/WorkoutAgentic.Api/ src/api/WorkoutAgentic.Api/
COPY docs/legal/ docs/legal/
RUN dotnet publish src/api/WorkoutAgentic.Api/WorkoutAgentic.Api.csproj --configuration Release --no-restore --output /publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Railway monta el volumen con propietario root. El umask protege base y claves.
USER root
RUN mkdir -p /data
COPY --from=api-build /publish/ ./
COPY --from=web-build /web/dist/web/browser/ ./wwwroot/
ENV ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Sqlite="Data Source=/data/workoutagentic.db;Cache=Shared" \
    Auth__DataProtectionKeysPath=/data/auth-keys
EXPOSE 8080
CMD ["sh", "-c", "umask 077; exec dotnet WorkoutAgentic.Api.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]
