namespace PermisoSalidaEquipos.Web.Authorization
{
    /// <summary>
    /// Nombres canónicos de los tres roles de aplicación. Deben coincidir exactamente
    /// con los valores sembrados en la tabla Roles (ver Data/DbInitializer.cs).
    /// </summary>
    public static class RoleNames
    {
        public const string Usuario = "Usuario";
        public const string JefeInmediato = "JefeInmediato";
        public const string DirectorTI = "DirectorTI";

        /// <summary>
        /// Guarda de seguridad de la portería: consulta las solicitudes ya aprobadas
        /// que aún no han salido físicamente de la empresa y confirma la salida. No
        /// se asigna a una persona en particular (los guardas rotan por turno):
        /// basta con dejar su NombreCompleto como "Guarda de Seguridad".
        /// </summary>
        public const string GuardaSeguridad = "GuardaSeguridad";

        /// <summary>
        /// Roles que no necesitan un jefe inmediato asignado para completar su
        /// perfil: el Director de TI (tope de la jerarquía) y el Guarda de
        /// Seguridad (cuenta operativa compartida, sin línea de reporte propia en
        /// este flujo).
        /// </summary>
        public static bool ExentoDeJefeInmediato(string? rol) => rol == DirectorTI || rol == GuardaSeguridad;

        /// <summary>
        /// Nombre del rol tal como se le muestra a las personas (con espacios y
        /// tildes), a diferencia del valor guardado en la base de datos, que debe
        /// quedar sin espacios para poder compararse de forma exacta en el código.
        /// </summary>
        public static string NombreAmigable(string? rol) => rol switch
        {
            Usuario => "Usuario",
            JefeInmediato => "Jefe Inmediato",
            DirectorTI => "Director de TI",
            GuardaSeguridad => "Guarda de Seguridad",
            _ => rol ?? string.Empty
        };
    }

    /// <summary>Nombres de las políticas de autorización registradas en Program.cs.</summary>
    public static class PolicyNames
    {
        public const string RequiereJefeInmediato = "RequiereJefeInmediato";
        public const string RequiereDirectorTI = "RequiereDirectorTI";
        public const string RequiereGuardaSeguridad = "RequiereGuardaSeguridad";
    }
}
