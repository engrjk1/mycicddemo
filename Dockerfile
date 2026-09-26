# Build stage: compile and publish the web app.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/MyCare.Web/MyCare.Web.csproj src/MyCare.Web/
RUN dotnet restore src/MyCare.Web/MyCare.Web.csproj
COPY src/ src/
RUN dotnet publish src/MyCare.Web/MyCare.Web.csproj -c Release -o /app --no-restore

# Runtime stage: small image with just the published app.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Render passes the commit being deployed as a build arg; /api/health reports it so the pipeline knows when the new version is live.
ARG RENDER_GIT_COMMIT=""
ENV ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Default="Data Source=/tmp/mycare.db" \
    RENDER_GIT_COMMIT=$RENDER_GIT_COMMIT
# Render passes the port in $PORT (default 10000); Program.cs listens on it.
EXPOSE 10000
USER app
ENTRYPOINT ["dotnet", "MyCare.Web.dll"]
