#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ARG UID=1003
ARG GID=1003

RUN addgroup --gid $GID appgroup && \
    adduser -u $UID --ingroup appgroup --disabled-password appuser

USER appuser

WORKDIR /app
EXPOSE 9000
EXPOSE 9001

ENV ASPNETCORE_HTTP_PORTS=9000
#ENV ASPNETCORE_HTTPS_PORTS=9001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MatchmakingServer/MatchmakingServer.csproj", "MatchmakingServer/"]
COPY ["H2MLauncher.Core/H2MLauncher.Core.csproj", "H2MLauncher.Core/"]
RUN dotnet restore "./MatchmakingServer/MatchmakingServer.csproj"
RUN dotnet restore "./H2MLauncher.Core/H2MLauncher.Core.csproj"
COPY ./MatchmakingServer/ ./MatchmakingServer
COPY ./H2MLauncher.Core/ ./H2MLauncher.Core
WORKDIR "/src/MatchmakingServer"
RUN dotnet build "./MatchmakingServer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MatchmakingServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MatchmakingServer.dll"]