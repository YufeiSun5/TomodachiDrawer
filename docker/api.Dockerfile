FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/TomodachiDrawerCn.Contracts/TomodachiDrawerCn.Contracts.csproj backend/TomodachiDrawerCn.Contracts/
COPY backend/TomodachiDrawerCn.Api/TomodachiDrawerCn.Api.csproj backend/TomodachiDrawerCn.Api/
RUN dotnet restore backend/TomodachiDrawerCn.Api/TomodachiDrawerCn.Api.csproj
COPY backend backend
RUN dotnet publish backend/TomodachiDrawerCn.Api/TomodachiDrawerCn.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV TOMODACHI_DATA_ROOT=/data
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TomodachiDrawerCn.Api.dll"]
