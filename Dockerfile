# RaceDay MVC web application (Part 3)
#
# Build from the repository root:
#   docker build -t raceday-web .
# Run (the API must be running and reachable from the container):
#   docker run -d -p 8080:8080 -e RaceDayApi__BaseUrl=http://host.docker.internal:5104/ --name raceday-web raceday-web

# ---------- Build stage: restore and publish with the .NET SDK ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first, so the restore layer is cached until the dependencies change.
COPY src/RaceDay.Web/RaceDay.Web.csproj src/RaceDay.Web/
RUN dotnet restore src/RaceDay.Web/RaceDay.Web.csproj

COPY src/RaceDay.Web/ src/RaceDay.Web/
RUN dotnet publish src/RaceDay.Web/RaceDay.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Runtime stage: small ASP.NET Core runtime image, no SDK ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    RaceDayApi__BaseUrl=http://host.docker.internal:5104/

EXPOSE 8080
COPY --from=build /app/publish .

# The .NET 8 images include a non-root "app" user; the site does not need root.
USER $APP_UID
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
