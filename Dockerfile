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
COPY --from=build /app/publish .
EXPOSE 4000
ENTRYPOINT ["dotnet", "LOGIN.dll"]
