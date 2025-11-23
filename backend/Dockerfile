# ========== build stage ==========
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

# restore
COPY ./src ./src
COPY ./tests ./tests
COPY ./Directory.Build.props ./
RUN dotnet restore ./src/Bot/Bot.csproj \
 && dotnet restore ./src/Worker/Worker.csproj

# publish (Release, self-contained=false)
RUN dotnet publish ./src/Bot/Bot.csproj -c Release -o /out/bot --no-restore \
 && dotnet publish ./src/Worker/Worker.csproj -c Release -o /out/worker --no-restore

# ========== runtime base ==========
FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim AS base
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    TZ=Asia/Almaty
RUN apt-get update && apt-get install -y --no-install-recommends \
      tzdata ca-certificates curl \
    && rm -rf /var/lib/apt/lists/* \
    && ln -fs /usr/share/zoneinfo/$TZ /etc/localtime && dpkg-reconfigure -f noninteractive tzdata

# non-root user
RUN useradd -ms /bin/bash app && mkdir -p /app/data /app/logs && chown -R app:app /app
USER app
WORKDIR /app

# ========== bot image ==========
FROM base AS bot
COPY --from=build /out/bot/ /app/
ENTRYPOINT ["dotnet","Bot.dll"]

# ========== worker image ==========
FROM base AS worker
COPY --from=build /out/worker/ /app/
ENTRYPOINT ["dotnet","Worker.dll"]
