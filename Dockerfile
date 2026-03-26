# Build context: repository root (device-data-manager).
# Cross-compile: build stage runs on $BUILDPLATFORM (native) so `dotnet publish` is not emulated under QEMU.
# ARM64 IoT Edge:
#   docker buildx build --platform linux/arm64 -t <registry>/device-data-manager:<tag> --push .
# AMD64:
#   docker buildx build --platform linux/amd64 -t <registry>/device-data-manager:<tag> --push .
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
# Map Docker TARGETARCH to .NET RID (defaults to amd64 if unset)
RUN RID=linux-x64; \
    if [ "$TARGETARCH" = "arm64" ]; then RID=linux-arm64; fi; \
    dotnet publish src/DeviceDataManager.Module/DeviceDataManager.Module.csproj \
      -c Release -o /app/publish -r "$RID" --self-contained false /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
# IoT SDK stores Edge CA certs under $HOME (OpenSslDirectoryBasedStoreProvider). useradd -m creates a writable home.
RUN groupadd -r -g 10001 moduleuser && \
    useradd -r -m -u 10001 -g moduleuser -d /home/moduleuser -s /usr/sbin/nologin moduleuser
COPY --from=build /app/publish .
RUN chown -R moduleuser:moduleuser /app
USER moduleuser
ENV HOME=/home/moduleuser
ENTRYPOINT ["dotnet", "DeviceDataManager.Module.dll"]
