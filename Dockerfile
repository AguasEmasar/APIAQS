# Etapa base: configuración de entorno
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:4000

# Instalar curl para healthcheck (solo necesario en runtime)
RUN apt-get update && apt-get install -y curl

WORKDIR /src

# Copiar solo los archivos necesarios para restaurar dependencias
COPY ["LOGIN/LOGIN.csproj", "LOGIN/"]
RUN dotnet restore "LOGIN/LOGIN.csproj"

# Copiar el resto del código fuente
COPY . .
WORKDIR /src/LOGIN
RUN dotnet publish "LOGIN.csproj" -c Release -o /app/publish

# Etapa final: imagen de runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Variables de entorno
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:4000

WORKDIR /app

# Instalar curl para healthcheck
RUN apt-get update && apt-get install -y curl && apt-get clean

# Health check
HEALTHCHECK --interval=30s --timeout=30s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:4000/health || exit 1

# Copiar la app publicada
COPY --from=build /app/publish .

# Exponer el puerto
EXPOSE 4000

# Ejecutar la aplicación
ENTRYPOINT ["dotnet", "LOGIN.dll"]
