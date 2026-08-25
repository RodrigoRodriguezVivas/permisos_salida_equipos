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
    }

    /// <summary>Nombres de las políticas de autorización registradas en Program.cs.</summary>
    public static class PolicyNames
    {
        public const string RequiereJefeInmediato = "RequiereJefeInmediato";
        public const string RequiereDirectorTI = "RequiereDirectorTI";
    }
}
