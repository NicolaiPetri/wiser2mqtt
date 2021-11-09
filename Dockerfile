#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
RUN apt update && apt install -y curl
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["wiser2mqtt.csproj", "."]
RUN dotnet restore "./wiser2mqtt.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "wiser2mqtt.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "wiser2mqtt.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "wiser2mqtt.dll"]