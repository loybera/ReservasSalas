# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los archivos de proyecto: la capa de restore se cachea mientras no cambien los paquetes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ReservasSalas.Api/ReservasSalas.Api.csproj src/ReservasSalas.Api/
RUN dotnet restore src/ReservasSalas.Api/ReservasSalas.Api.csproj

COPY src/ src/
RUN dotnet publish src/ReservasSalas.Api/ReservasSalas.Api.csproj -c Release -o /app --no-restore

# Etapa final: solo el runtime de ASP.NET, sin SDK
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Usuario sin privilegios que trae la imagen base
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ReservasSalas.Api.dll"]
