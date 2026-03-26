# Build context: repository root (device-data-manager).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/DeviceDataManager.Module/DeviceDataManager.Module.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
RUN groupadd --gid 1000 moduleuser && useradd --uid 1000 --gid moduleuser --shell /bin/false moduleuser
USER moduleuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DeviceDataManager.Module.dll"]
