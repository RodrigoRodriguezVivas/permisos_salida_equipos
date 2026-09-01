# Permisos de Salida de Equipos de Cómputo — Alianza Gráfica S.A.

Aplicación web para gestionar el permiso de salida de equipos de cómputo, con
flujo de aprobación en dos pasos (jefe inmediato → Director de TI) más
confirmación física de salida en portería (Guarda de Seguridad), ingreso
automático con la cuenta de dominio (Windows/Active Directory, con nombre,
correo y cargo autocompletados desde el directorio) y datos almacenados en
SQL Server. Construida en ASP.NET Core 8 MVC para hospedarse en el IIS de
Aligraf.

## 1. Qué incluye

- `PermisoSalidaEquipos.Web/` — el proyecto ASP.NET Core 8 MVC.
- `database/01_CreateDatabase.sql` — script para crear la base de datos y las
  tablas en SQL Server (coincide exactamente con lo que espera la aplicación).
- `Dockerfile`, `.dockerignore`, `render.yaml` — solo para publicar el sitio de
  **demostración** pública (ver sección 10). El despliegue real en el IIS de
  Aligraf no los usa.
- Este `README.md`.

## 2. Cómo funciona el flujo

1. **Usuario**: inicia sesión (automáticamente, con su cuenta de Windows). En
   el primer ingreso, la aplicación consulta Active Directory y autocompleta
   nombre completo, correo y cargo (si AD no responde, o esos datos no están
   diligenciados allí, la persona los completa a mano; siempre puede volver a
   traerlos con el botón "Traer mis datos desde Active Directory"). Solo la
   cédula y el jefe inmediato se piden siempre en "Completar perfil", porque
   Active Directory no los tiene. Desde "Nueva solicitud" registra el permiso
   de salida del equipo.
2. **Jefe inmediato**: recibe un correo y ve la solicitud en "Aprobaciones
   (jefe inmediato)". Puede aprobar (pasa al Director de TI) o rechazar.
3. **Director de TI**: recibe la solicitud aprobada por el jefe, la revisa en
   "Aprobaciones (Director TI)" y da la aprobación final o la rechaza.
4. **Guarda de Seguridad**: una vez la solicitud tiene aprobación final,
   queda visible en "Salidas (portería)" para cualquier guarda. Cuando el
   equipo efectivamente sale de las instalaciones, el guarda de turno lo
   busca ahí, verifica los datos contra el equipo físico y confirma la
   salida desde el detalle de la solicitud. Este rol no está ligado a una
   persona en particular (los guardas rotan por turno): el usuario de
   ejemplo se llama simplemente "Guarda de Seguridad", y en el despliegue
   real basta con crear una sola cuenta compartida de portería con este rol.
5. El solicitante recibe un correo en cada cambio de estado, incluyendo la
   confirmación de salida. Todo el historial queda visible en el detalle de
   cada solicitud.
6. El Director de TI tiene además acceso a **Administración** (para asignar
   el rol y el jefe inmediato de cada persona), **Reportes** (historial
   completo, filtros y exportación a Excel) y, al igual que el Guarda de
   Seguridad, a "Salidas (portería)" para poder hacer seguimiento.

Los cuatro roles de la aplicación (Usuario, JefeInmediato, DirectorTI,
GuardaSeguridad) son independientes de los grupos de Windows: se administran
desde la propia aplicación, en Administración > Usuarios, exclusivo del
Director de TI. Esa pantalla incluye un buscador contra Active Directory: el
Director de TI puede encontrar a cualquier persona del directorio de Aligraf
por nombre o usuario de dominio y agregarla con su rol de una vez, sin
esperar a que esa persona inicie sesión por primera vez.

## 3. Requisitos en el servidor (IIS de Aligraf)

1. **Windows Server con IIS**, con estos roles/características de IIS
   habilitados:
   - `Web Server (IIS) > Security > Windows Authentication`
   - Asegúrate de que `Anonymous Authentication` quede **deshabilitada** y
     `Windows Authentication` **habilitada** en el sitio/aplicación del
     Administrador de IIS (Authentication).
2. **.NET 8 Hosting Bundle** instalado en el servidor (no solo el runtime):
   <https://dotnet.microsoft.com/download/dotnet/8.0> → "Hosting Bundle".
   Después de instalarlo, ejecuta `net stop was /y` seguido de `net start w3svc`
   (o reinicia el servidor) para que IIS reconozca el módulo de ASP.NET Core.
3. Acceso desde el servidor IIS al **SQL Server** de Aligraf, instancia
   `SALAH\PRUEBAS`, donde vive la base de datos `Informes_Aligraf` que ya
   usan los demás desarrollos propios de Aligraf — este proyecto se instala
   ahí mismo (no crea una base de datos propia), con todas sus tablas
   identificadas por el prefijo `PS_`.
4. La cuenta bajo la que corre el Application Pool de IIS necesita permisos
   para autenticar contra Active Directory (normalmente basta con la cuenta
   de aplicación estándar del servidor; en caso de usar Kerberos en vez de
   NTLM, puede requerirse registrar un SPN — consúltalo con el equipo de
   infraestructura si el login no funciona).
5. Esa misma cuenta del Application Pool necesita permiso de **lectura**
   sobre el directorio (el mismo nivel que cualquier usuario autenticado del
   dominio normalmente ya tiene) para que la aplicación pueda traer nombre
   completo, correo y cargo de cada persona desde Active Directory. Si por
   política de Aligraf se prefiere usar una cuenta de servicio dedicada en
   vez de la identidad del Application Pool, diligénciala en
   `ActiveDirectory:Usuario` / `ActiveDirectory:Clave` (ver sección 5). Si
   esta consulta falla por cualquier motivo, la aplicación sigue funcionando
   igual: la persona simplemente diligencia esos datos a mano.

## 4. Base de datos

Este proyecto **no** crea su propia base de datos: se instala dentro de
`Informes_Aligraf` (instancia `SALAH\PRUEBAS`), la base de datos donde ya
viven los demás desarrollos propios de Aligraf. Para que sus tablas nunca
choquen con las de esos otros sistemas, **todas** las tablas, llaves e
índices de este proyecto llevan el prefijo `PS_` (`PS_Roles`, `PS_Usuarios`,
`PS_Solicitudes`, `PS_HistorialSolicitudes`, `PK_PS_...`, `FK_PS_...`,
`IX_PS_...`) — están completamente aisladas del resto de tablas de esa base
de datos.

1. En el SQL Server de Aligraf (instancia `SALAH\PRUEBAS`), ejecuta
   `database/01_CreateDatabase.sql` (por ejemplo desde SQL Server Management
   Studio) **contra la base de datos `Informes_Aligraf` ya existente**. El
   script hace `USE Informes_Aligraf` y solo crea las tablas `PS_*` — si esa
   base de datos no existe en esa instancia, el script se detiene con un
   error explícito en vez de crearla, porque está pensado para instalarse en
   una base de datos que ya existe.
2. (Opcional pero recomendado) Para poder entrar la primera vez y asignar
   los demás roles, deja algún usuario como Director de TI desde el
   principio. Hay dos formas — usa solo una:
   - Descomenta y ajusta el bloque `INSERT INTO dbo.PS_Usuarios` al final del
     script SQL con la cuenta de dominio real, **o**
   - Configura `AdministradorInicial:NombreUsuarioDominio` en
     `appsettings.json` (ver sección 5) con el formato `DOMINIO\usuario`
     antes del primer arranque; la aplicación lo creará automáticamente. Deja
     ese valor vacío después del primer despliegue.

## 5. Configuración (`appsettings.json`)

Antes de publicar, edita `PermisoSalidaEquipos.Web/appsettings.json` (o, mejor
aún, usa las variables de entorno / el `web.config` de IIS para no versionar
contraseñas):

```json
{
  "ConnectionStrings": {
    "PermisoSalidaEquiposDb": "Server=SALAH\\PRUEBAS;Database=Informes_Aligraf;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Smtp": {
    "Habilitado": true,
    "Host": "smtp.alianzagrafica.com",
    "Puerto": 25,
    "UsarSsl": false,
    "Usuario": "",
    "Clave": "",
    "CorreoRemitente": "no-responder@alianzagrafica.com",
    "UrlBaseAplicacion": "http://permisos.alianzagrafica.local"
  },
  "ActiveDirectory": {
    "Dominio": "",
    "Usuario": "",
    "Clave": ""
  }
}
```

Pide al equipo de infraestructura los datos reales del servidor SMTP interno
y de la cadena de conexión de SQL Server. Si `Smtp:Habilitado` queda en
`false`, la aplicación funciona igual pero no envía correos.

`ActiveDirectory:Dominio/Usuario/Clave` normalmente se dejan **vacíos**: la
aplicación consulta Active Directory con la identidad del Application Pool de
IIS (ver sección 3, punto 5), que en la mayoría de los casos ya tiene permiso
de lectura suficiente. Solo hace falta diligenciarlos si el equipo de
infraestructura pide usar una cuenta de servicio dedicada para esa consulta.

`Database:CrearEsquemaAutomaticamente` debe quedar en `false` en producción
(el esquema ya lo crea `01_CreateDatabase.sql`); solo se deja en `true` en
`appsettings.Development.json` para poder probar sin acceso al DBA.

## 6. Compilar y publicar

Esto requiere una máquina con el **SDK de .NET 8** y acceso a NuGet (para
restaurar `Microsoft.EntityFrameworkCore.SqlServer`,
`Microsoft.AspNetCore.Authentication.Negotiate`,
`System.DirectoryServices.AccountManagement`, `MailKit` y `ClosedXML` la
primera vez) — puede ser tu equipo de desarrollo o un servidor de build; no
hace falta que sea el servidor IIS final.

```bash
cd PermisoSalidaEquipos.Web
dotnet restore
dotnet publish -c Release -o ./publish
```

Copia el contenido de la carpeta `publish` al directorio del sitio en IIS
(por ejemplo `C:\inetpub\wwwroot\PermisosSalidaEquipos`), y crea en IIS:

- Un **Application Pool** con "No Managed Code" (.NET CLR Version = No
  Managed Code), modo de proceso según lo que uses (In-Process es lo
  recomendado y lo que trae este proyecto por defecto).
- Un **sitio o aplicación** apuntando a esa carpeta, con **Windows
  Authentication** habilitada y **Anonymous Authentication** deshabilitada
  (paso 3 arriba).

## 7. Primer ingreso

1. Entra a la URL del sitio con un usuario de dominio. La aplicación crea tu
   registro automáticamente y te pide completar cédula, cargo y jefe
   inmediato.
2. Si configuraste el `AdministradorInicial` (o el INSERT manual), esa cuenta
   ya tiene rol Director de TI y puede entrar a Administración > Usuarios
   para asignar el rol correcto (Usuario / JefeInmediato / DirectorTI) y el
   jefe inmediato de cada persona a medida que vayan ingresando.

## 8. Estructura del proyecto

```
PermisoSalidaEquipos.Web/
  Authorization/    Roles de aplicación, políticas y el filtro de "perfil completo"
  Controllers/       Home, Perfil, Solicitudes, Aprobaciones, Guardia, Admin, Reportes
  Data/               DbContext (EF Core) y siembra inicial de roles
  Models/             Entidades: Rol, Usuario, Solicitud, HistorialSolicitud, EstadoSolicitud
  Services/           Resolución del usuario actual (Windows→BD), consulta a Active Directory, envío de correo, notificaciones
  ViewModels/         Modelos de las vistas (formularios, listados, filtros)
  Views/              Razor views (Bootstrap 5, incluido localmente en wwwroot/lib)
  Program.cs          Autenticación Windows (Negotiate/IIS), EF Core, políticas de autorización
```

## 9. Nota sobre la verificación

Este proyecto se escribió y se revisó cuidadosamente, y la parte de lógica de
negocio (controladores, servicios, modelos, autorización) se verificó
compilando el código real contra sustitutos con la misma forma de API que los
paquetes NuGet reales (Entity Framework Core, autenticación Negociate,
MailKit, ClosedXML), ya que el entorno donde se generó este proyecto no tiene
salida a NuGet.org. Aun así, el primer `dotnet restore` en tu máquina de
desarrollo (paso 6) es el que finalmente compila contra los paquetes reales;
si algo no compila, es casi seguro un tema de versión de paquete y no de
lógica — dinos qué error da y lo ajustamos.

## 10. Publicar un sitio de DEMOSTRACIÓN gratuito (Render.com)

Esto NO es el despliegue real de Aligraf (ese es el de las secciones 3 a 7).
Es una versión pública para mostrar cómo se ve y se usa la aplicación, sin
depender del dominio de Windows ni de un SQL Server: usa un login donde
eliges con cuál usuario de ejemplo entrar (Usuario, Jefe Inmediato o Director
de TI) y una base de datos SQLite que se crea sola con datos de prueba
(varias solicitudes en distintos estados, para que se vea el flujo completo
desde el primer momento). Todo esto se activa solo con la variable de entorno
`ASPNETCORE_ENVIRONMENT=Demo`; el código de negocio es exactamente el mismo.

### Paso 1: Subir el proyecto a GitHub

Este proyecto ya viene como un repositorio git local con un primer commit
listo. Solo te falta crear el repositorio vacío en GitHub y conectarlo:

1. Entra a <https://github.com/new>, dale un nombre (por ejemplo
   `permiso-salida-equipos-demo`), y créalo **vacío** (sin README, sin
   .gitignore — ya los trae este proyecto). Puede ser público o privado; si
   es privado, Render te va a pedir autorizar el acceso a ese repositorio
   específico en el paso 2.
2. En una terminal, dentro de la carpeta de este proyecto:

   ```bash
   git remote add origin https://github.com/TU-USUARIO/permiso-salida-equipos-demo.git
   git branch -M main
   git push -u origin main
   ```

   (Reemplaza `TU-USUARIO` y el nombre del repositorio por los tuyos. Si te
   pide iniciar sesión, usa tu usuario de GitHub y, en vez de tu contraseña,
   un *Personal Access Token* — GitHub ya no acepta la contraseña normal por
   línea de comandos; lo puedes crear en
   <https://github.com/settings/tokens>.)

### Paso 2: Crear la cuenta en Render.com y desplegar

1. Ve a <https://render.com> y crea una cuenta gratuita (puedes entrar
   directamente con tu cuenta de GitHub — no pide tarjeta de crédito para el
   plan gratuito).
2. En el dashboard, click en **New +** → **Web Service**.
3. Conecta tu cuenta de GitHub y selecciona el repositorio que subiste en el
   Paso 1.
4. Render va a detectar el `Dockerfile` automáticamente y va a mostrar
   "Environment: Docker" — déjalo así.
5. En **Instance Type**, elige **Free**.
6. En **Environment Variables**, agrega:
   - `ASPNETCORE_ENVIRONMENT` = `Demo`

   (El Dockerfile ya trae este valor por defecto, así que este paso es
   opcional, pero es buena idea dejarlo explícito.)
7. Click en **Create Web Service**. Render va a construir la imagen Docker
   (ahí sí se descargan los paquetes NuGet reales, sin restricciones de red)
   y publicar el sitio. La primera vez tarda unos minutos.
8. Cuando termine, Render te da una URL pública tipo
   `https://permiso-salida-equipos-demo.onrender.com`. Esa es la demo.

### Cosas a tener en cuenta del plan gratuito de Render

- El servicio se "duerme" después de ~15 minutos sin tráfico, y la próxima
  visita tarda unos 30-60 segundos en despertar — normal en el plan gratuito.
- El plan gratuito no tiene disco persistente: cada vez que el servicio se
  reinicia (se duerme y despierta, o hay un nuevo despliegue), la base de
  datos SQLite se recrea desde cero con los mismos datos de ejemplo. Es lo
  esperado para una demo: siempre arranca "limpia".
- Si más adelante quieres actualizar la demo, basta con hacer `git push` de
  nuevo a la misma rama — Render vuelve a construir y desplegar solo.
- Los usuarios de ejemplo son: **Rodrigo Rodriguez Vivas** (Director de TI),
  **Juan Gabriel Silva** (Jefe de Producción), **Laura Gómez** / **Julián
  Rojas** (Usuario), y **Guarda de Seguridad** (portería, cuenta genérica sin
  nombre propio ya que los guardas rotan por turno). Entrando como cada uno
  se puede ver todo el flujo: crear una solicitud, aprobarla como jefe,
  aprobarla como Director de TI, el rechazo en cualquiera de los dos pasos,
  y — para una solicitud ya aprobada — confirmar en "Salidas (portería)" que
  el equipo salió físicamente de la empresa.
