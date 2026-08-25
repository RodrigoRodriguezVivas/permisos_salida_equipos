using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using PermisoSalidaEquipos.Web.Authorization;
using PermisoSalidaEquipos.Web.Data;
using PermisoSalidaEquipos.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// ModoDemo: cuando es true (ver appsettings.Demo.json, activado con
// ASPNETCORE_ENVIRONMENT=Demo) la aplicación corre en modo de demostración
// pública: login simulado (sin Active Directory) y base de datos SQLite
// autocontenida en vez de SQL Server. Es exactamente el mismo código de
// negocio; solo cambian la autenticación y el proveedor de base de datos.
// En el despliegue real de Aligraf este valor queda en false (o ausente) y
// se usa Windows Authentication + SQL Server como siempre.
// ---------------------------------------------------------------------
var modoDemo = builder.Configuration.GetValue<bool>("ModoDemo");

if (modoDemo)
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Cuenta/Ingresar";
            options.AccessDeniedPath = "/Cuenta/Ingresar";
            options.ExpireTimeSpan = TimeSpan.FromHours(4);
        });
}
else
{
    // Autenticación integrada de Windows (SSO): el usuario ya inició sesión
    // en su equipo con su cuenta de dominio; IIS negocia Kerberos/NTLM y
    // entrega la identidad de Windows a la aplicación sin pedir usuario y
    // contraseña de nuevo. Requiere habilitar "Windows Authentication" y
    // deshabilitar "Anonymous Authentication" en el sitio/aplicación de IIS
    // (ver README.md).
    builder.Services.AddAuthentication(Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme)
        .AddNegotiate();

    // Cuando se hospeda en IIS mediante el módulo ASP.NET Core, IIS ya hizo
    // la negociación de Windows Authentication; esto asegura que el esquema
    // de IIS quede configurado correctamente en el servidor.
    builder.Services.Configure<Microsoft.AspNetCore.Builder.IISServerOptions>(options =>
    {
        options.AutomaticAuthentication = true;
    });
}

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy; // exige usuario autenticado en toda la app salvo lo marcado [AllowAnonymous]

    options.AddPolicy(PolicyNames.RequiereJefeInmediato, policy =>
        policy.Requirements.Add(new RoleRequirement(RoleNames.JefeInmediato)));

    options.AddPolicy(PolicyNames.RequiereDirectorTI, policy =>
        policy.Requirements.Add(new RoleRequirement(RoleNames.DirectorTI)));

    options.AddPolicy(PolicyNames.RequiereGuardaSeguridad, policy =>
        policy.Requirements.Add(new RoleRequirement(RoleNames.GuardaSeguridad)));
});

builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, RoleAuthorizationHandler>();

// ---------------------------------------------------------------------
// Base de datos vía Entity Framework Core. En producción (Aligraf) es SQL
// Server; en modo demo es SQLite, un solo archivo sin necesidad de un
// servidor de base de datos aparte, ideal para un hosting gratuito.
// ---------------------------------------------------------------------
var proveedorBaseDatos = builder.Configuration["Database:Proveedor"] ?? "SqlServer";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (string.Equals(proveedorBaseDatos, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("PermisoSalidaEquiposDb"));
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("PermisoSalidaEquiposDb"));
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<NotificacionService>();

builder.Services.AddControllersWithViews(options =>
{
    // Filtro global: obliga a completar el perfil (cédula, cargo, jefe inmediato)
    // antes de usar cualquier otra pantalla de la aplicación.
    options.Filters.Add<PerfilCompletoFilter>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var crearEsquemaAutomaticamente = app.Configuration.GetValue<bool>("Database:CrearEsquemaAutomaticamente");
    var administradorInicial = app.Configuration["AdministradorInicial:NombreUsuarioDominio"];
    await DbInitializer.InicializarAsync(db, administradorInicial, crearEsquemaAutomaticamente);

    if (modoDemo)
    {
        await DbInitializer.SembrarDatosDemoAsync(db);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
