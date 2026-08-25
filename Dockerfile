# Imagen para el sitio de DEMOSTRACIÓN pública (Render.com u otro hosting con
# Docker). Usa ASPNETCORE_ENVIRONMENT=Demo para activar el login simulado y la
# base de datos SQLite (ver PermisoSalidaEquipos.Web/appsettings.Demo.json).
# El despliegue real en el IIS de Aligraf NO usa este Dockerfile: se publica
# directamente en IIS siguiendo el README.md.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY PermisoSalidaEquipos.Web/PermisoSalidaEquipos.Web.csproj PermisoSalidaEquipos.Web/
RUN dotnet restore PermisoSalidaEquipos.Web/PermisoSalidaEquipos.Web.csproj -r linux-x64

COPY PermisoSalidaEquipos.Web/ PermisoSalidaEquipos.Web/
WORKDIR /src/PermisoSalidaEquipos.Web
# -r linux-x64: publicación explícita para Linux/x64, para que la librería
# nativa que usa SQLite (Microsoft.Data.Sqlite) quede empacada sin ambigüedad.
RUN dotnet publish -c Release -r linux-x64 --self-contained false -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Demo
# Render (y la mayoría de hostings gratuitos) inyectan el puerto a usar en la
# variable de entorno PORT; si no está definida (por ejemplo al probar la
# imagen en local) se usa 8080 por defecto.
ENV PORT=8080
EXPOSE 8080

# Evita que el JIT de .NET 8 se caiga (SIGABRT / "Exited with status 134")
# en plataformas que aíslan los contenedores con gVisor o mecanismos
# similares (Render, Google Cloud Run, etc.), donde la nueva protección de
# memoria "write-xor-execute" del JIT no es totalmente compatible.
ENV DOTNET_EnableWriteXorExecute=0

# El recolector de basura "Server" (el que usa .NET por defecto) reserva un
# segmento de memoria grande por cada núcleo que detecta, asumiendo un
# servidor con varios núcleos y bastante RAM disponible. En una instancia
# gratuita muy pequeña (Render Free = 512 MB) eso puede agotar la memoria
# apenas arranca el proceso y provocar el mismo error 134. El modo
# "Workstation" (no concurrente) usa muchísima menos memoria.
ENV DOTNET_gcServer=0
ENV DOTNET_gcConcurrent=0

# CAUSA REAL del error 134 confirmada por el log de Render: por defecto,
# ASP.NET Core deja un FileSystemWatcher vigilando appsettings.json para
# poder recargar la configuración si el archivo cambia. El entorno aislado
# de Render no soporta bien "inotify" (el mecanismo de Linux para vigilar
# archivos) y la app se cae apenas lo intenta, antes de terminar de
# arrancar. Esto desactiva esa recarga en caliente (no se necesita para una
# demo: si algo de configuración cambiara, de todas formas hay que
# reconstruir y volver a desplegar la imagen).
ENV DOTNET_hostBuilder__reloadConfigOnChange=false

ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} dotnet PermisoSalidaEquipos.Web.dll"]
