# Guía de despliegue en producción — PASE (Permisos de Salida de Equipos)

Esta guía recoge, ya depurada, la secuencia completa que usamos para poner la
aplicación a funcionar en el servidor de pruebas `PRUEBAS` — incluyendo las
soluciones a cada problema que fuimos encontrando en el camino (autenticación,
dominio, base de datos compartida, permisos). Síguela en orden en el servidor
real de producción para evitar repetir el mismo proceso de prueba y error.

Datos que ya sabemos, confirmados en las pruebas:

- El dominio de Active Directory real de la empresa es **`GRUPOGRAF`** (no
  "ALIANZAGRAFICA", aunque ese sea el nombre comercial).
- La base de datos definitiva es **`Informes_Aligraf`**, en el servidor SQL
  Server, instancia **`SALAH\PRUEBAS`**, compartida con otros desarrollos
  propios de Aligraf — por eso todas las tablas de este proyecto llevan el
  prefijo `PS_`.
- El primer Director de TI configurado es **`GRUPOGRAF\dir_tecnologia`**.
- Kerberos falla en este entorno (probablemente por no existir un SPN
  registrado para el nombre del sitio) — hay que forzar **NTLM** como único
  proveedor de autenticación de Windows.

---

## 0. Antes de empezar: qué necesitas tener a mano

- Acceso de administrador al servidor de producción por Escritorio Remoto (RDP).
- Acceso de administrador a la instancia SQL Server `SALAH\PRUEBAS` (SQL Server
  Management Studio).
- El nombre exacto del servidor de producción (ej. `SRV-APPS01`) y su IP.
- Idealmente, una cuenta de servicio de dominio dedicada para esta aplicación
  (ej. `GRUPOGRAF\svc_pase`). Si no la tienes todavía, pídesela al equipo de
  infraestructura — evita usar una cuenta personal o de administrador como
  identidad de la aplicación en producción.
- El nombre que va a usar la aplicación puertas afuera, ej.
  `permisos.alianzagrafica.com` (o el que decida infraestructura).

---

## 1. Compilar y publicar (en tu equipo de desarrollo)

```
cd "C:\Users\dir_tecnologia\OneDrive - Alianza Grafica S.A\Director\Proyectos\Permiso salida equipos de computo\PermisoSalidaEquipos_Demo\PermisoSalidaEquipos.Web"
dotnet publish -c Release -o ./publish
```

Verifica que la fecha del `.dll` recién generado es la de ahora mismo (para
tener un punto de referencia y poder confirmar más adelante que la copia al
servidor sí se actualizó):

```
(Get-Item ".\publish\PermisoSalidaEquipos.Web.dll").LastWriteTime
```

Antes de continuar, confirma también que `appsettings.json` (el de tu
repositorio, no el del servidor) tiene los valores correctos de producción:

```
notepad .\appsettings.json
```

- `ConnectionStrings:PermisoSalidaEquiposDb` →
  `Server=SALAH\PRUEBAS;Database=Informes_Aligraf;Trusted_Connection=True;TrustServerCertificate=True;`
- `AdministradorInicial:NombreUsuarioDominio` → `GRUPOGRAF\dir_tecnologia`

Si estos dos valores ya están bien en el repositorio (deberían estarlo, ya
los corregimos), no hace falta tocar nada — simplemente ten presente que **no
hay que editarlos manualmente en el servidor esta vez**, porque el servidor
real usa la misma base de datos que ya está configurada en el repositorio
(a diferencia del servidor de pruebas, donde sí tocaba ajustar la cadena de
conexión a mano porque la base de datos de prueba vivía en otra instancia).

---

## 2. Instalar el Hosting Bundle de ASP.NET Core (en el servidor, si no está)

**En el servidor de producción, por RDP**, PowerShell como administrador:

```
Get-Item "C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll" -ErrorAction SilentlyContinue
```

Si no muestra nada, descarga el **Hosting Bundle** de ASP.NET Core Runtime 8.0
desde `https://dotnet.microsoft.com/download/dotnet/8.0` (sección "Run apps -
Runtime", botón "Hosting Bundle") e instálalo. Luego:

```
net stop was /y
net start w3svc
```

---

## 3. Copiar la aplicación al servidor (desde tu equipo)

Ajusta el nombre del servidor real en la ruta:

```
robocopy "C:\Users\dir_tecnologia\OneDrive - Alianza Grafica S.A\Director\Proyectos\Permiso salida equipos de computo\PermisoSalidaEquipos_Demo\PermisoSalidaEquipos.Web\publish" "\\NOMBRE_SERVIDOR\c$\inetpub\PermisoSalidaEquipos" /MIR /R:3 /W:5
```

Verifica el resultado (columna "Failed" en 0), y confirma que el `.dll` en el
servidor quedó con la misma fecha que el de tu equipo (Paso 1):

```
(Get-Item "C:\inetpub\PermisoSalidaEquipos\PermisoSalidaEquipos.Web.dll").LastWriteTime
```

Permisos NTFS (normalmente se heredan bien al estar bajo `inetpub`, pero
confírmalo):

```
icacls "C:\inetpub\PermisoSalidaEquipos"
```

Debe aparecer `IIS_IUSRS` con `(RX)`. Si no:

```
icacls "C:\inetpub\PermisoSalidaEquipos" /grant "IIS_IUSRS:(OI)(CI)RX" /T
```

---

## 4. Crear el Application Pool con una cuenta de servicio de dominio

**En el servidor, PowerShell como administrador:**

```
Import-Module WebAdministration
New-WebAppPool -Name "PermisoSalidaEquipos"
Set-ItemProperty IIS:\AppPools\PermisoSalidaEquipos -Name managedRuntimeVersion -Value ""
Set-ItemProperty IIS:\AppPools\PermisoSalidaEquipos -Name processModel.identityType -Value SpecificUser
Set-ItemProperty IIS:\AppPools\PermisoSalidaEquipos -Name processModel.userName -Value "GRUPOGRAF\svc_pase"
Set-ItemProperty IIS:\AppPools\PermisoSalidaEquipos -Name processModel.password -Value "la_clave_de_esa_cuenta"
```

Usar una cuenta de dominio real (no la identidad por defecto del Application
Pool) evita el problema que tuvimos en pruebas, donde la identidad virtual se
presentaba ante SQL Server como la cuenta de equipo (`DOMINIO\NOMBREEQUIPO$`)
y SQL Server la rechazaba.

---

## 5. Dar acceso a esa cuenta en SQL Server

En SQL Server Management Studio, conectado a `SALAH\PRUEBAS`:

```sql
USE [master];
GO
CREATE LOGIN [GRUPOGRAF\svc_pase] FROM WINDOWS;
GO
USE [Informes_Aligraf];
GO
CREATE USER [GRUPOGRAF\svc_pase] FOR LOGIN [GRUPOGRAF\svc_pase];
GO
ALTER ROLE db_datareader ADD MEMBER [GRUPOGRAF\svc_pase];
ALTER ROLE db_datawriter ADD MEMBER [GRUPOGRAF\svc_pase];
GO
```

Si la cuenta ya existe como login (por ejemplo porque ya tenía acceso previo),
el `CREATE LOGIN` puede fallar diciendo que ya existe — en ese caso, sáltate
esa línea y ejecuta desde `USE [Informes_Aligraf];` en adelante.

---

## 6. Crear las tablas en la base de datos (si no se ha hecho ya)

En SSMS, conectado a `SALAH\PRUEBAS`, ejecuta el script completo
`database/01_CreateDatabase.sql` del repositorio. Crea las tablas
`PS_Roles`, `PS_Usuarios`, `PS_Solicitudes` y `PS_HistorialSolicitudes` dentro
de `Informes_Aligraf` (el script no crea la base de datos, solo valida que
exista y agrega las tablas).

---

## 7. Crear el sitio en IIS

**En el servidor, PowerShell:**

```
Import-Module WebAdministration
New-Website -Name "PermisoSalidaEquipos" -PhysicalPath "C:\inetpub\PermisoSalidaEquipos" -ApplicationPool "PermisoSalidaEquipos" -Port 80 -HostHeader "permisos.alianzagrafica.com"
```

Si da error de "el elemento ya existe" (por ejemplo si ya habías hecho una
prueba en este mismo servidor), en vez de crear uno nuevo corrige el binding
del existente:

```
Get-WebBinding -Name "PermisoSalidaEquipos" | Remove-WebBinding
New-WebBinding -Name "PermisoSalidaEquipos" -Protocol http -Port 80 -HostHeader "permisos.alianzagrafica.com"
Set-ItemProperty "IIS:\Sites\PermisoSalidaEquipos" -Name physicalPath -Value "C:\inetpub\PermisoSalidaEquipos"
Set-ItemProperty "IIS:\Sites\PermisoSalidaEquipos" -Name applicationPool -Value "PermisoSalidaEquipos"
```

---

## 8. Autenticación de Windows: activarla, desactivar la anónima, y forzar NTLM

```
Import-Module WebAdministration
Set-WebConfigurationProperty -Filter /system.webServer/security/authentication/windowsAuthentication -Name enabled -Value true -PSPath "IIS:\Sites\PermisoSalidaEquipos"
Set-WebConfigurationProperty -Filter /system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value false -PSPath "IIS:\Sites\PermisoSalidaEquipos"
```

Fuerza NTLM (evita el bucle de credenciales que da Kerberos sin un SPN
registrado). Si el primer comando da error de "sección bloqueada", desbloquéala
primero con `appcmd`:

```
& "$env:windir\system32\inetsrv\appcmd.exe" unlock config -section:system.webServer/security/authentication/windowsAuthentication
Clear-WebConfiguration -Filter "/system.webServer/security/authentication/windowsAuthentication/providers" -PSPath "IIS:\Sites\PermisoSalidaEquipos"
Add-WebConfiguration -Filter "/system.webServer/security/authentication/windowsAuthentication/providers" -Value @{value='NTLM'} -PSPath "IIS:\Sites\PermisoSalidaEquipos"
```

Verifica el resultado:

```
Get-WebConfiguration -Filter "/system.webServer/security/authentication/windowsAuthentication/providers/*" -PSPath "IIS:\Sites\PermisoSalidaEquipos" | Select-Object value
```

Debe mostrar solo `NTLM`.

---

## 9. DNS y firewall — para que TODA la red pueda entrar

Este paso es el que falta para que cualquier persona de Aligraf entre desde su
propio equipo (no solo tú desde el servidor). Se usa `permisos.alianzagrafica.com`
en vez de un sufijo `.local` — el mismo dominio público de los correos de la
empresa, pero resuelto de forma privada solo dentro de la red de Aligraf
("split-horizon DNS"). Esto dos ventajas: es más fácil de recordar/escribir, y
más adelante se puede usar un certificado HTTPS público real (algo que un
nombre `.local` nunca puede tener).

**a) Averigua primero si ya existe una zona DNS interna para `alianzagrafica.com`.**
Conectado al servidor DNS interno de Aligraf (normalmente un controlador de
dominio), PowerShell como administrador:

```
Get-DnsServerZone | Where-Object { $_.ZoneName -like "*alianzagrafica*" }
```

- **Si aparece una zona llamada exactamente `alianzagrafica.com`** (ya
  gestionada internamente, cosa común si el dominio de Active Directory usa
  ese mismo nombre DNS), simplemente agrega el registro `permisos` dentro de
  esa zona:

```
Add-DnsServerResourceRecordA -Name "permisos" -ZoneName "alianzagrafica.com" -IPv4Address "IP_DEL_SERVIDOR_IIS"
```

- **Si NO existe esa zona**, **no la crees completa** — crear una zona interna
  para todo `alianzagrafica.com` "taparía" también la resolución de cualquier
  otro nombre real bajo ese dominio (como el sitio web público o el correo),
  a menos que también los repliques ahí manualmente. En su lugar, crea una
  zona interna **solo para el subdominio específico** `permisos.alianzagrafica.com`,
  que no afecta a nada más:

```
Add-DnsServerPrimaryZone -Name "permisos.alianzagrafica.com" -ReplicationScope "Domain"
Add-DnsServerResourceRecordA -Name "@" -ZoneName "permisos.alianzagrafica.com" -IPv4Address "IP_DEL_SERVIDOR_IIS"
```

Si el DNS lo administra otra persona del equipo de infraestructura, pídeles
exactamente uno de estos dos registros (según si ya existe o no la zona
`alianzagrafica.com` internamente) para `permisos.alianzagrafica.com` →
`<IP real del servidor de producción>`.

**b) Abre el puerto 80 en el firewall de Windows del servidor**, para que
otros equipos de la red puedan llegar (por defecto IIS no siempre tiene el
puerto expuesto a toda la red):

```
New-NetFirewallRule -DisplayName "PASE - HTTP 80" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow
```

**c) (Opcional pero recomendado a mediano plazo) HTTPS.** Por ahora la
aplicación queda en HTTP simple, que es aceptable para una app interna con
autenticación de Windows (las credenciales no viajan en texto plano gracias al
protocolo NTLM). Si más adelante quieres cifrar también el resto del tráfico,
pide un certificado interno al equipo de infraestructura y agrega un binding
HTTPS (puerto 443) al sitio.

**Nota importante:** a diferencia del servidor de pruebas, en el servidor real
de producción **no deberías necesitar** el ajuste de "loopback check"
(`BackConnectionHostNames`) — ese problema solo aparece cuando alguien navega
al sitio *desde el mismo servidor que lo hospeda*. Los usuarios reales van a
entrar desde sus propios equipos, así que no deberían toparse con eso. Solo
aplícalo si tú mismo pruebas por RDP desde el servidor y ves el mismo bucle de
credenciales que vimos en pruebas.

---

## 10. Primera prueba (activa el log temporalmente)

Antes de la primera prueba, activa el log de arranque por si algo falla:

```
mkdir C:\inetpub\PermisoSalidaEquipos\logs
icacls "C:\inetpub\PermisoSalidaEquipos\logs" /grant "IIS_IUSRS:(OI)(CI)F" /T
notepad C:\inetpub\PermisoSalidaEquipos\web.config
```

Cambia `stdoutLogEnabled="false"` a `"true"`, guarda, y reinicia:

```
Start-WebAppPool -Name "PermisoSalidaEquipos"
```

Desde **otro equipo de la red** (no el servidor), entra a
`http://permisos.alianzagrafica.com` con `GRUPOGRAF\dir_tecnologia`.

Si algo falla, revisa el log más reciente:

```
Get-ChildItem C:\inetpub\PermisoSalidaEquipos\logs | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | Get-Content
```

Una vez que todo funcione bien, **desactiva el log** de nuevo (para no llenar
el disco con el tiempo):

```
notepad C:\inetpub\PermisoSalidaEquipos\web.config
```
(`stdoutLogEnabled="true"` → `"false"`, y reinicia el Application Pool).

---

## 11. Después de confirmar que todo funciona

1. En Administración → Usuarios y roles, usa "Buscar y agregar desde AD" para
   ir agregando a los Jefes Inmediatos, al resto del equipo de TI (con rol
   Director de TI si aplica) y al Guarda de Seguridad, asignándoles su rol.
2. Comunica a la empresa la URL (`http://permisos.alianzagrafica.com`) para
   que empiecen a usarla.
3. Si vas a usar el envío de correos (`Smtp:Habilitado`), pide al equipo de
   infraestructura los datos reales del servidor SMTP interno y configúralos
   en `appsettings.json` del servidor.

---

## Resumen de causas de error que ya resolvimos (por si reaparecen)

| Síntoma | Causa | Solución |
|---|---|---|
| Cuadro de credenciales en bucle, aunque sean correctas | Kerberos sin SPN registrado | Forzar NTLM (Paso 8) |
| Cuadro de credenciales en bucle, probando desde el propio servidor por RDP | "Loopback check" de Windows | Agregar el nombre a `BackConnectionHostNames` (solo si pruebas desde el servidor mismo) |
| Error 500.30 al abrir el sitio | La app .NET no arrancó | Activar `stdoutLogEnabled` y revisar el log (Paso 10) |
| `Login failed for user 'DOMINIO\NOMBREEQUIPO$'` | Application Pool con identidad virtual, sin cuenta de dominio propia | Usar una cuenta de servicio de dominio como identidad del pool (Paso 4) + darle acceso en SQL (Paso 5) |
| `El nombre de objeto 'Roles' no es válido` (o similar con cualquier tabla) | El `.dll` desplegado es una versión vieja del código | Volver a `dotnet publish` + copiar, y confirmar con `LastWriteTime` que el archivo del servidor cambió |
| `No se puede abrir la base de datos 'X'` | La cadena de conexión apunta a una base o servidor equivocado | Revisar `appsettings.json` del servidor línea por línea |
| Application Pool no arranca (`Restart-WebAppPool` falla) | Rapid-Fail Protection lo detuvo por fallos repetidos | `Start-WebAppPool` en vez de `Restart-WebAppPool`, después de corregir la causa real del fallo |
