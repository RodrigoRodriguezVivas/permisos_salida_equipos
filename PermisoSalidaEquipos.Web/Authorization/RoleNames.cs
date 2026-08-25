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
        /// Nombre del rol tal como se le muestra a las personas (con espacios y
        /// tildes), a diferencia del valor guardado en la base de datos, que debe
        /// quedar sin espacios para poder compararse de forma exacta en el código.
        /// </summary>
        public static string NombreAmigable(string? rol) => rol switch
        {
            Usuario => "Usuario",
            JefeInmediato => "Jefe Inmediato",
            DirectorTI => "Director de TI",
            _ => rol ?? string.Empty
        };
    }

    /// <summary>Nombres de las políticas de autorización registradas en Program.cs.</summary>
    public static class PolicyNames
    {
        public const string RequiereJefeInmediato = "RequiereJefeInmediato";
        public const string RequiereDirectorTI = "RequiereDirectorTI";
    }
}
