# RaceDay API (Part 2, with Azure Blob Storage added in Part 3)
# Build from the repository root:
#   docker build -f src/RaceDay.Api/Dockerfile -t raceday-api .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/RaceDay.Api/RaceDay.Api.csproj src/RaceDay.Api/
RUN dotnet restore src/RaceDay.Api/RaceDay.Api.csproj

COPY src/RaceDay.Api/ src/RaceDay.Api/
RUN dotnet publish src/RaceDay.Api/RaceDay.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
