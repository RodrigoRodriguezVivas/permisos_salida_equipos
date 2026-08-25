/* =====================================================================
   Permisos de Salida de Equipos de Cómputo - Alianza Gráfica S.A.
   Script de creación de base de datos y esquema para SQL Server.

   Ejecutar como un usuario con permisos suficientes (p. ej. sysadmin o
   dbcreator + db_owner sobre la base de datos creada). El esquema de
   tablas coincide exactamente con el mapeado por Entity Framework Core
   en Data/ApplicationDbContext.cs, así que la aplicación puede usarse
   directamente contra esta base sin ejecutar migraciones.
   ===================================================================== */

IF DB_ID(N'PermisoSalidaEquiposDb') IS NULL
BEGIN
    CREATE DATABASE PermisoSalidaEquiposDb;
END
GO

USE PermisoSalidaEquiposDb;
GO

IF OBJECT_ID(N'dbo.HistorialSolicitudes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        Id      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        Nombre  NVARCHAR(50)      NOT NULL
    );
    CREATE UNIQUE INDEX IX_Roles_Nombre ON dbo.Roles(Nombre);

    CREATE TABLE dbo.Usuarios
    (
        Id                     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        NombreUsuarioDominio   NVARCHAR(256)      NOT NULL,
        NombreCompleto         NVARCHAR(200)      NOT NULL,
        Correo                 NVARCHAR(256)      NOT NULL DEFAULT(''),
        Cedula                 NVARCHAR(30)        NULL,
        Cargo                  NVARCHAR(150)       NULL,
        RolId                  INT                NOT NULL CONSTRAINT FK_Usuarios_Roles REFERENCES dbo.Roles(Id),
        JefeInmediatoId        INT                 NULL CONSTRAINT FK_Usuarios_JefeInmediato REFERENCES dbo.Usuarios(Id),
        Activo                 BIT                NOT NULL DEFAULT(1),
        FechaCreacion          DATETIME2          NOT NULL DEFAULT(SYSDATETIME())
    );
    CREATE UNIQUE INDEX IX_Usuarios_NombreUsuarioDominio ON dbo.Usuarios(NombreUsuarioDominio);
    CREATE INDEX IX_Usuarios_JefeInmediatoId ON dbo.Usuarios(JefeInmediatoId);
    CREATE INDEX IX_Usuarios_RolId ON dbo.Usuarios(RolId);

    CREATE TABLE dbo.Solicitudes
    (
        Id                     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Solicitudes PRIMARY KEY,
        SolicitanteId          INT                NOT NULL CONSTRAINT FK_Solicitudes_Solicitante REFERENCES dbo.Usuarios(Id),
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

        JefeInmediatoId        INT                NOT NULL CONSTRAINT FK_Solicitudes_JefeInmediato REFERENCES dbo.Usuarios(Id),
        FechaDecisionJefe      DATETIME2           NULL,
        ComentarioJefe         NVARCHAR(500)        NULL,

        DirectorTIRevisorId    INT                 NULL CONSTRAINT FK_Solicitudes_DirectorTI REFERENCES dbo.Usuarios(Id),
        FechaDecisionDirectorTI DATETIME2          NULL,
        ComentarioDirectorTI   NVARCHAR(500)        NULL,

        RegistradaSalidaPorId  INT                 NULL CONSTRAINT FK_Solicitudes_RegistradaSalidaPor REFERENCES dbo.Usuarios(Id),
        FechaSalidaRegistrada  DATETIME2           NULL,
        ComentarioGuarda       NVARCHAR(500)        NULL
    );
    CREATE INDEX IX_Solicitudes_SolicitanteId ON dbo.Solicitudes(SolicitanteId);
    CREATE INDEX IX_Solicitudes_JefeInmediatoId ON dbo.Solicitudes(JefeInmediatoId);
    CREATE INDEX IX_Solicitudes_DirectorTIRevisorId ON dbo.Solicitudes(DirectorTIRevisorId);
    CREATE INDEX IX_Solicitudes_Estado ON dbo.Solicitudes(Estado);

    CREATE TABLE dbo.HistorialSolicitudes
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HistorialSolicitudes PRIMARY KEY,
        SolicitudId   INT                NOT NULL CONSTRAINT FK_Historial_Solicitud REFERENCES dbo.Solicitudes(Id) ON DELETE CASCADE,
        Estado        INT                NOT NULL,
        UsuarioId     INT                NOT NULL CONSTRAINT FK_Historial_Usuario REFERENCES dbo.Usuarios(Id),
        Fecha         DATETIME2          NOT NULL DEFAULT(SYSDATETIME()),
        Comentario    NVARCHAR(500)       NULL
    );
    CREATE INDEX IX_HistorialSolicitudes_SolicitudId ON dbo.HistorialSolicitudes(SolicitudId);
END
GO

/* ---------------------------------------------------------------------
   Si la base de datos ya existía de una instalación anterior (antes de que
   existiera el rol Guarda de Seguridad), estas columnas y este rol pueden
   faltar. Estos bloques agregan lo que falte sin afectar los datos
   existentes; en una instalación nueva ya quedan creados por el CREATE
   TABLE de arriba, así que aquí no hacen nada.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Solicitudes') AND name = 'RegistradaSalidaPorId')
BEGIN
    ALTER TABLE dbo.Solicitudes ADD RegistradaSalidaPorId INT NULL CONSTRAINT FK_Solicitudes_RegistradaSalidaPor REFERENCES dbo.Usuarios(Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Solicitudes') AND name = 'FechaSalidaRegistrada')
BEGIN
    ALTER TABLE dbo.Solicitudes ADD FechaSalidaRegistrada DATETIME2 NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Solicitudes') AND name = 'ComentarioGuarda')
BEGIN
    ALTER TABLE dbo.Solicitudes ADD ComentarioGuarda NVARCHAR(500) NULL;
END
GO

/* ---------------------------------------------------------------------
   Siembra de los cuatro roles fijos de la aplicación. Cada uno se agrega
   de forma independiente (no solo cuando la tabla Roles está vacía), para
   que una base de datos ya provisionada anteriormente también reciba el
   rol Guarda de Seguridad al ejecutar este script de nuevo.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Nombre = 'Usuario')
BEGIN
    INSERT INTO dbo.Roles (Nombre) VALUES ('Usuario');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Nombre = 'JefeInmediato')
BEGIN
    INSERT INTO dbo.Roles (Nombre) VALUES ('JefeInmediato');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Nombre = 'DirectorTI')
BEGIN
    INSERT INTO dbo.Roles (Nombre) VALUES ('DirectorTI');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Nombre = 'GuardaSeguridad')
BEGIN
    INSERT INTO dbo.Roles (Nombre) VALUES ('GuardaSeguridad');
END
GO

/* ---------------------------------------------------------------------
   OPCIONAL: primer usuario con rol Director de TI, para poder entrar la
   primera vez y asignar los demás roles desde Administración > Usuarios.
   Reemplaza 'ALIANZAGRAFICA\usuario.director' por la cuenta de dominio
   real. También se puede lograr configurando
   AdministradorInicial:NombreUsuarioDominio en appsettings.json y
   dejando que la aplicación lo cree en el primer arranque.
   --------------------------------------------------------------------- */
-- IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE NombreUsuarioDominio = 'ALIANZAGRAFICA\usuario.director')
-- BEGIN
--     INSERT INTO dbo.Usuarios (NombreUsuarioDominio, NombreCompleto, Correo, Cedula, Cargo, RolId, Activo, FechaCreacion)
--     SELECT 'ALIANZAGRAFICA\usuario.director', 'Director de TI', 'director.tecnologia@alianzagrafica.com', '0000000000', 'Director de TI',
--            (SELECT Id FROM dbo.Roles WHERE Nombre = 'DirectorTI'), 1, SYSDATETIME();
-- END
-- GO
