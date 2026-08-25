# Imagen para el sitio de DEMOSTRACIÓN pública (Render.com u otro hosting con
# Docker). Usa ASPNETCORE_ENVIRONMENT=Demo para activar el login simulado y la
# base de datos SQLite (ver PermisoSalidaEquipos.Web/appsettings.Demo.json).
# El despliegue real en el IIS de Aligraf NO usa este Dockerfile: se publica
# directamente en IIS siguiendo el README.md.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PermisoSalidaEquipos.Web/PermisoSalidaEquipos.Web.csproj PermisoSalidaEquipos.Web/
RUN dotnet restore PermisoSalidaEquipos.Web/PermisoSalidaEquipos.Web.csproj

COPY PermisoSalidaEquipos.Web/ PermisoSalidaEquipos.Web/
WORKDIR /src/PermisoSalidaEquipos.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Demo
# Render (y la mayoría de hostings gratuitos) inyectan el puerto a usar en la
# variable de entorno PORT; si no está definida (por ejemplo al probar la
# imagen en local) se usa 8080 por defecto.
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} dotnet PermisoSalidaEquipos.Web.dll"]
