FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
# RocksDB 6.2.2 native library dependencies + libdl.so symlink for NativeImport
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       libsnappy1v5 \
       liblz4-1 \
       libzstd1 \
       libbz2-1.0 \
       zlib1g \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
RUN ln -sf "$(find /lib /usr/lib -name 'libdl.so.2' | head -1)" /app/libdl.so
ENTRYPOINT ["dotnet", "RocksDb-Demo.dll"]
