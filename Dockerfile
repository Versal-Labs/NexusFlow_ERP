FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore NexusFlow_ERP.sln
RUN dotnet publish NexusFlow.Web/NexusFlow.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    NEXUSFLOW_INSTANCE_ROOT=/app/state \
    NEXUSFLOW_DEPLOYMENT_PROFILE=PortableVm \
    NEXUSFLOW_SECRET_STORE=EncryptedFile \
    NEXUSFLOW_STATE_STORE=File \
    NEXUSFLOW_DATA_PROTECTION_STORE=File \
    NEXUSFLOW_STORAGE_MODE=Local

RUN mkdir -p /app/state
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "NexusFlow.Web.dll"]
