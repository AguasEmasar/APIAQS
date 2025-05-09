# Etapa de construcción
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solo los archivos necesarios para restaurar dependencias
COPY ["LOGIN/LOGIN.csproj", "LOGIN/"]
RUN dotnet restore "LOGIN/LOGIN.csproj"

# Copiar todo y construir
COPY . . 
RUN dotnet publish "LOGIN/LOGIN.csproj" -c Release -o /app/publish

# Etapa final
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Configuración de health check
HEALTHCHECK --interval=30s --timeout=30s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:4000/health || exit 1

COPY --from=build /app/publish . 
EXPOSE 4000
ENTRYPOINT ["dotnet", "LOGIN.dll"]
