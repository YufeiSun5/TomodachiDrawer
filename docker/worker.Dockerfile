FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/TomodachiDrawerCn.Contracts/TomodachiDrawerCn.Contracts.csproj backend/TomodachiDrawerCn.Contracts/
COPY backend/TomodachiDrawerCn.Worker/TomodachiDrawerCn.Worker.csproj backend/TomodachiDrawerCn.Worker/
RUN dotnet restore backend/TomodachiDrawerCn.Worker/TomodachiDrawerCn.Worker.csproj
COPY backend backend
RUN dotnet publish backend/TomodachiDrawerCn.Worker/TomodachiDrawerCn.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV TOMODACHI_DATA_ROOT=/data
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TomodachiDrawerCn.Worker.dll"]
