using System;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermisoSalidaEquipos.Web.Authorization;
using PermisoSalidaEquipos.Web.Data;
using PermisoSalidaEquipos.Web.Models;
using PermisoSalidaEquipos.Web.ViewModels;

namespace PermisoSalidaEquipos.Web.Controllers
{
    /// <summary>
    /// Historial y reportes de todas las solicitudes, exclusivo del Director de TI.
    /// </summary>
    [Authorize(Policy = PolicyNames.RequiereDirectorTI)]
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ReportesController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(ReporteFiltroViewModel filtro)
        {
            filtro.Resultados = await ConsultarAsync(filtro).ToListAsync();
            return View(filtro);
        }

        public async Task<IActionResult> ExportarExcel(ReporteFiltroViewModel filtro)
        {
            var solicitudes = await ConsultarDetalladoAsync(filtro).ToListAsync();

            using var libro = new XLWorkbook();
            var hoja = libro.Worksheets.Add("Permisos de salida");

            string[] encabezados =
            {
                "Id", "Solicitante", "Cédula", "Cargo", "Tipo de equipo", "Marca", "Modelo", "Serie",
                "Motivo", "Fecha de salida", "Fecha retorno estimada", "Estado", "Fecha de creación",
                "Jefe inmediato", "Fecha decisión jefe", "Comentario jefe",
                "Director TI", "Fecha decisión director", "Comentario director",
                "Salida confirmada por (portería)", "Fecha salida confirmada", "Observaciones portería"
            };
            for (var i = 0; i < encabezados.Length; i++)
            {
                hoja.Cell(1, i + 1).Value = encabezados[i];
                hoja.Cell(1, i + 1).Style.Font.Bold = true;
            }

            var fila = 2;
            foreach (var s in solicitudes)
            {
                hoja.Cell(fila, 1).Value = s.Id;
                hoja.Cell(fila, 2).Value = s.Solicitante?.NombreCompleto;
                hoja.Cell(fila, 3).Value = s.CedulaSolicitante;
                hoja.Cell(fila, 4).Value = s.CargoSolicitante;
                hoja.Cell(fila, 5).Value = s.TipoEquipo;
                hoja.Cell(fila, 6).Value = s.Marca;
                hoja.Cell(fila, 7).Value = s.Modelo;
                hoja.Cell(fila, 8).Value = s.NumeroSerie;
                hoja.Cell(fila, 9).Value = s.Motivo;
                hoja.Cell(fila, 10).Value = s.FechaSalida;
                hoja.Cell(fila, 11).Value = s.FechaRetornoEstimada;
                hoja.Cell(fila, 12).Value = EstadoSolicitudTexto.Descripcion(s.Estado);
                hoja.Cell(fila, 13).Value = s.FechaCreacion;
                hoja.Cell(fila, 14).Value = s.JefeInmediatoAsignado?.NombreCompleto;
                hoja.Cell(fila, 15).Value = s.FechaDecisionJefe;
                hoja.Cell(fila, 16).Value = s.ComentarioJefe;
                hoja.Cell(fila, 17).Value = s.DirectorTIRevisor?.NombreCompleto;
                hoja.Cell(fila, 18).Value = s.FechaDecisionDirectorTI;
                hoja.Cell(fila, 19).Value = s.ComentarioDirectorTI;
                hoja.Cell(fila, 20).Value = s.RegistradaSalidaPor?.NombreCompleto;
                hoja.Cell(fila, 21).Value = s.FechaSalidaRegistrada;
                hoja.Cell(fila, 22).Value = s.ComentarioGuarda;
                fila++;
            }

            hoja.Columns().AdjustToContents();

            using var stream = new System.IO.MemoryStream();
            libro.SaveAs(stream);
            var nombreArchivo = $"Permisos_Salida_Equipos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
        }

        private IQueryable<SolicitudListItemViewModel> ConsultarAsync(ReporteFiltroViewModel filtro)
        {
            return AplicarFiltros(_db.Solicitudes.AsQueryable(), filtro)
                .OrderByDescending(s => s.FechaCreacion)
                .Select(s => new SolicitudListItemViewModel
                {
                    Id = s.Id,
                    SolicitanteNombre = s.Solicitante!.NombreCompleto,
                    TipoEquipo = s.TipoEquipo,
                    Marca = s.Marca,
                    Modelo = s.Modelo,
                    FechaSalida = s.FechaSalida,
                    FechaCreacion = s.FechaCreacion,
                    Estado = s.Estado
                });
        }

        private IQueryable<Solicitud> ConsultarDetalladoAsync(ReporteFiltroViewModel filtro)
        {
            return AplicarFiltros(
                _db.Solicitudes
                    .Include(s => s.Solicitante)
                    .Include(s => s.JefeInmediatoAsignado)
                    .Include(s => s.DirectorTIRevisor)
                    .Include(s => s.RegistradaSalidaPor)
                    .AsQueryable(),
                filtro)
                .OrderByDescending(s => s.FechaCreacion);
        }

        private static IQueryable<Solicitud> AplicarFiltros(IQueryable<Solicitud> query, ReporteFiltroViewModel filtro)
        {
            if (filtro.Desde.HasValue)
            {
                query = query.Where(s => s.FechaCreacion >= filtro.Desde.Value.Date);
            }

            if (filtro.Hasta.HasValue)
            {
                var hastaFin = filtro.Hasta.Value.Date.AddDays(1);
                query = query.Where(s => s.FechaCreacion < hastaFin);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Estado) && Enum.TryParse<EstadoSolicitud>(filtro.Estado, out var estado))
            {
                query = query.Where(s => s.Estado == estado);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Solicitante))
            {
                query = query.Where(s => s.Solicitante!.NombreCompleto.Contains(filtro.Solicitante));
            }

            return query;
        }
    }
}
