# Etapa de construcción
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solo los archivos necesarios para restaurar dependencias
COPY ["LOGIN/LOGIN.csproj", "LOGIN/"]
RUN dotnet restore "LOGIN/LOGIN.csproj"

# Copiar el resto de los archivos del proyecto y publicar
COPY . ./
RUN dotnet publish "LOGIN/LOGIN.csproj" -c Release -o /app/publish

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copiar los archivos publicados de la etapa de construcción
COPY --from=build /app/publish .

# Exponer el puerto en el que la aplicación escucha
EXPOSE 4000

# Comando para ejecutar la aplicación
ENTRYPOINT ["dotnet", "LOGIN.dll"]
