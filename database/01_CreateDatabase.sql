/* =====================================================================
   Permisos de Salida de Equipos de Cómputo - Alianza Gráfica S.A.
   Script de creación de tablas para SQL Server.

   Este proyecto NO usa una base de datos propia: se instala dentro de la
   base de datos ya existente Informes_Aligraf (instancia SALAH\PRUEBAS),
   donde viven los demás desarrollos propios de Aligraf. Por eso este
   script no crea ninguna base de datos, solo tablas — y todas llevan el
   prefijo "PS_" (Permisos de Salida) en el nombre, tanto las tablas como
   sus llaves primarias, foráneas e índices, para no chocar con las tablas
   de ningún otro sistema que viva en esa misma base de datos.

   Ejecutar como un usuario con permisos suficientes sobre Informes_Aligraf
   (p. ej. db_owner o, como mínimo, permiso para crear tablas e índices).
   El esquema de tablas coincide exactamente con el mapeado por Entity
   Framework Core en Data/ApplicationDbContext.cs, así que la aplicación
   puede usarse directamente contra esta base sin ejecutar migraciones.
   ===================================================================== */

IF DB_ID(N'Informes_Aligraf') IS NULL
BEGIN
    RAISERROR(N'La base de datos Informes_Aligraf no existe en esta instancia. Este script no la crea a propósito: está pensado para instalarse dentro de una base de datos ya existente.', 16, 1);
    RETURN;
END
GO

USE Informes_Aligraf;
GO

IF OBJECT_ID(N'dbo.PS_HistorialSolicitudes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PS_Roles
    (
        Id      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PS_Roles PRIMARY KEY,
        Nombre  NVARCHAR(50)      NOT NULL
    );
    CREATE UNIQUE INDEX IX_PS_Roles_Nombre ON dbo.PS_Roles(Nombre);

    CREATE TABLE dbo.PS_Usuarios
    (
        Id                     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PS_Usuarios PRIMARY KEY,
        NombreUsuarioDominio   NVARCHAR(256)      NOT NULL,
        NombreCompleto         NVARCHAR(200)      NOT NULL,
        Correo                 NVARCHAR(256)      NOT NULL DEFAULT(''),
        Cedula                 NVARCHAR(30)        NULL,
        Cargo                  NVARCHAR(150)       NULL,
        RolId                  INT                NOT NULL CONSTRAINT FK_PS_Usuarios_PS_Roles REFERENCES dbo.PS_Roles(Id),
        JefeInmediatoId        INT                 NULL CONSTRAINT FK_PS_Usuarios_JefeInmediato REFERENCES dbo.PS_Usuarios(Id),
        Activo                 BIT                NOT NULL DEFAULT(1),
        FechaCreacion          DATETIME2          NOT NULL DEFAULT(SYSDATETIME())
    );
    CREATE UNIQUE INDEX IX_PS_Usuarios_NombreUsuarioDominio ON dbo.PS_Usuarios(NombreUsuarioDominio);
    CREATE INDEX IX_PS_Usuarios_JefeInmediatoId ON dbo.PS_Usuarios(JefeInmediatoId);
    CREATE INDEX IX_PS_Usuarios_RolId ON dbo.PS_Usuarios(RolId);

    CREATE TABLE dbo.PS_Solicitudes
    (
        Id                     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PS_Solicitudes PRIMARY KEY,
        SolicitanteId          INT                NOT NULL CONSTRAINT FK_PS_Solicitudes_Solicitante REFERENCES dbo.PS_Usuarios(Id),
        CedulaSolicitante      NVARCHAR(30)       NOT NULL,
        CargoSolicitante       NVARCHAR(150)      NOT NULL,

        TipoEquipo             NVARCHAR(100)      NOT NULL,
        Marca                  NVARCHAR(100)      NOT NULL,
        Modelo                 NVARCHAR(100)      NOT NULL,
        NumeroSerie            NVARCHAR(100)      NOT NULL,
        Accesorios             NVARCHAR(500)       NULL,

        Motivo                 NVARCHAR(100)      NOT NULL,
        MotivoDetalle          NVARCHAR(500)       NULL,
        FechaSalida            DATETIME2          NOT NULL,
        FechaRetornoEstimada   DATETIME2           NULL,
        Observaciones          NVARCHAR(500)       NULL,

        Estado                 INT                NOT NULL, -- 0 PendienteJefe, 1 PendienteDirectorTI, 2 Aprobada, 3 RechazadaJefe, 4 RechazadaDirectorTI, 5 CanceladaPorSolicitante, 6 SalioDeLaEmpresa
        FechaCreacion          DATETIME2          NOT NULL DEFAULT(SYSDATETIME()),

        JefeInmediatoId        INT                NOT NULL CONSTRAINT FK_PS_Solicitudes_JefeInmediato REFERENCES dbo.PS_Usuarios(Id),
        FechaDecisionJefe      DATETIME2           NULL,
        ComentarioJefe         NVARCHAR(500)        NULL,

        DirectorTIRevisorId    INT                 NULL CONSTRAINT FK_PS_Solicitudes_DirectorTI REFERENCES dbo.PS_Usuarios(Id),
        FechaDecisionDirectorTI DATETIME2          NULL,
        ComentarioDirectorTI   NVARCHAR(500)        NULL,

        RegistradaSalidaPorId  INT                 NULL CONSTRAINT FK_PS_Solicitudes_RegistradaSalidaPor REFERENCES dbo.PS_Usuarios(Id),
        FechaSalidaRegistrada  DATETIME2           NULL,
        ComentarioGuarda       NVARCHAR(500)        NULL
    );
    CREATE INDEX IX_PS_Solicitudes_SolicitanteId ON dbo.PS_Solicitudes(SolicitanteId);
    CREATE INDEX IX_PS_Solicitudes_JefeInmediatoId ON dbo.PS_Solicitudes(JefeInmediatoId);
    CREATE INDEX IX_PS_Solicitudes_DirectorTIRevisorId ON dbo.PS_Solicitudes(DirectorTIRevisorId);
    CREATE INDEX IX_PS_Solicitudes_Estado ON dbo.PS_Solicitudes(Estado);

    CREATE TABLE dbo.PS_HistorialSolicitudes
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PS_HistorialSolicitudes PRIMARY KEY,
        SolicitudId   INT                NOT NULL CONSTRAINT FK_PS_Historial_Solicitud REFERENCES dbo.PS_Solicitudes(Id) ON DELETE CASCADE,
        Estado        INT                NOT NULL,
        UsuarioId     INT                NOT NULL CONSTRAINT FK_PS_Historial_Usuario REFERENCES dbo.PS_Usuarios(Id),
        Fecha         DATETIME2          NOT NULL DEFAULT(SYSDATETIME()),
        Comentario    NVARCHAR(500)       NULL
    );
    CREATE INDEX IX_PS_HistorialSolicitudes_SolicitudId ON dbo.PS_HistorialSolicitudes(SolicitudId);
END
GO

/* ---------------------------------------------------------------------
   Si las tablas ya existían de una instalación anterior (antes de que
   existiera el rol Guarda de Seguridad), estas columnas y este rol pueden
   faltar. Estos bloques agregan lo que falte sin afectar los datos
   existentes; en una instalación nueva ya quedan creados por el CREATE
   TABLE de arriba, así que aquí no hacen nada.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.PS_Solicitudes') AND name = 'RegistradaSalidaPorId')
BEGIN
    ALTER TABLE dbo.PS_Solicitudes ADD RegistradaSalidaPorId INT NULL CONSTRAINT FK_PS_Solicitudes_RegistradaSalidaPor REFERENCES dbo.PS_Usuarios(Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.PS_Solicitudes') AND name = 'FechaSalidaRegistrada')
BEGIN
    ALTER TABLE dbo.PS_Solicitudes ADD FechaSalidaRegistrada DATETIME2 NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.PS_Solicitudes') AND name = 'ComentarioGuarda')
BEGIN
    ALTER TABLE dbo.PS_Solicitudes ADD ComentarioGuarda NVARCHAR(500) NULL;
END
GO

/* ---------------------------------------------------------------------
   Siembra de los cuatro roles fijos de la aplicación. Cada uno se agrega
   de forma independiente (no solo cuando la tabla PS_Roles está vacía),
   para que una instalación ya provisionada anteriormente también reciba
   el rol Guarda de Seguridad al ejecutar este script de nuevo.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.PS_Roles WHERE Nombre = 'Usuario')
BEGIN
    INSERT INTO dbo.PS_Roles (Nombre) VALUES ('Usuario');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PS_Roles WHERE Nombre = 'JefeInmediato')
BEGIN
    INSERT INTO dbo.PS_Roles (Nombre) VALUES ('JefeInmediato');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PS_Roles WHERE Nombre = 'DirectorTI')
BEGIN
    INSERT INTO dbo.PS_Roles (Nombre) VALUES ('DirectorTI');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PS_Roles WHERE Nombre = 'GuardaSeguridad')
BEGIN
    INSERT INTO dbo.PS_Roles (Nombre) VALUES ('GuardaSeguridad');
END
GO

/* ---------------------------------------------------------------------
   OPCIONAL: primer usuario con rol Director de TI, para poder entrar la
   primera vez y asignar los demás roles desde Administración > Usuarios.
   Reemplaza 'GRUPOGRAF\dir_tecnologia' por la cuenta de dominio real (el
   dominio real de Aligraf es GRUPOGRAF, no ALIANZAGRAFICA). También se
   puede lograr configurando AdministradorInicial:NombreUsuarioDominio en
   appsettings.json y dejando que la aplicación lo cree en el primer
   arranque (así quedó configurado por defecto en este proyecto).
   --------------------------------------------------------------------- */
-- IF NOT EXISTS (SELECT 1 FROM dbo.PS_Usuarios WHERE NombreUsuarioDominio = 'GRUPOGRAF\dir_tecnologia')
-- BEGIN
--     INSERT INTO dbo.PS_Usuarios (NombreUsuarioDominio, NombreCompleto, Correo, Cedula, Cargo, RolId, Activo, FechaCreacion)
--     SELECT 'GRUPOGRAF\dir_tecnologia', 'Director de TI', 'director.tecnologia@alianzagrafica.com', '0000000000', 'Director de TI',
--            (SELECT Id FROM dbo.PS_Roles WHERE Nombre = 'DirectorTI'), 1, SYSDATETIME();
-- END
-- GO
