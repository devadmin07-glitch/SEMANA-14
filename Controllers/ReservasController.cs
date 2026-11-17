using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HotelReservasMVC.Data;
using HotelReservasMVC.Models;
using HotelReservasMVC.ViewModels;

namespace HotelReservasMVC.Controllers;

public class ReservasController(SistemaDbContext db) : Controller
{
    private const decimal IgvTasa=0.18m; // Supuesto explícito para el prototipo, editable.

    public async Task<IActionResult> Index()
    {
        var rows=await db.Reservas.AsNoTracking().Include(x=>x.Cliente).Include(x=>x.Detalles)
            .OrderByDescending(x=>x.Id).Take(50).ToListAsync();
        return View(rows);
    }
    [HttpGet]
    public async Task<IActionResult> Nueva(int? clienteId)
    {
        await CargarClientes();
        return View(new ReservaVm { ClienteId=clienteId ?? 0 });
    }
    // La ECU distingue Disponible, Reservado y Ocupado. Solo se ofrece una
    // habitacion Disponible. Al editar se permiten las que ya pertenecen a
    // la reserva actual, aunque su estado sea Reservado.
    [HttpGet]
    public async Task<IActionResult> HabitacionesDisponibles(
        DateTime llegada, DateTime salida, int? excluirReservaId)
    {
        llegada = llegada.Date;
        salida = salida.Date;
        if (llegada < DateTime.Today || salida <= llegada)
            return BadRequest("Seleccione fechas validas: la salida debe ser posterior a la llegada.");

        var propias = excluirReservaId.HasValue
            ? await db.DetallesReserva.AsNoTracking()
                .Where(d => d.ReservaId == excluirReservaId.Value)
                .Select(d => d.HabitacionId).ToListAsync()
            : new List<int>();

        var conCruce = await db.DetallesReserva.AsNoTracking()
            .Where(d => (!excluirReservaId.HasValue || d.ReservaId != excluirReservaId.Value)
                     && d.Reserva.FechaLlegada < salida
                     && d.Reserva.FechaSalida > llegada)
            .Select(d => d.HabitacionId).Distinct().ToListAsync();

        var habitaciones = await db.Habitaciones.AsNoTracking()
            .OrderBy(h => h.Numero).ToListAsync();

        var idsPropios = propias.ToHashSet();
        var idsConCruce = conCruce.ToHashSet();
        var resultado = habitaciones.Select(h =>
        {
            var esPropia = idsPropios.Contains(h.Id);
            var conflicto = idsConCruce.Contains(h.Id);
            var disponible = !conflicto &&
                (h.Estado == "Disponible" || (esPropia && h.Estado == "Reservado"));
            // Si una reserva anterior coincide con estas fechas, nunca
            // presentar la habitacion como Disponible aunque SQL este desactualizado.
            var estado = conflicto && h.Estado == "Disponible" ? "Reservado" : h.Estado;
            return new
            {
                id = h.Id, numero = h.Numero, tipo = h.Tipo,
                costoDia = h.CostoDia, estado, disponible, esPropia
            };
        }).ToList();

        return Json(new { habitaciones = resultado });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nueva(ReservaVm model)
    {
        model.FechaLlegada=model.FechaLlegada.Date;
        model.FechaSalida=model.FechaSalida.Date;
        if (model.FechaLlegada<DateTime.Today || model.FechaSalida<=model.FechaLlegada)
            ModelState.AddModelError("", "Las fechas deben ser futuras/vigentes y la salida posterior a la llegada.");
        if (model.Habitaciones.Count==0) ModelState.AddModelError("", "Agregue habitaciones al detalle.");
        if (model.Habitaciones.Select(x=>x.HabitacionId).Distinct().Count()!=model.Habitaciones.Count)
            ModelState.AddModelError("", "No puede duplicar una habitación en la misma reserva.");
        if (!ModelState.IsValid) { await CargarClientes();return View(model); }
        if (!await db.Clientes.AnyAsync(x=>x.Id==model.ClienteId))
        { ModelState.AddModelError("","Cliente no existe. Registre al cliente para continuar."); await CargarClientes();return View(model); }
        var dias=(model.FechaSalida-model.FechaLlegada).Days;
        var ids=model.Habitaciones.Select(x=>x.HabitacionId).ToList();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var rooms=await db.Habitaciones.Where(h=>ids.Contains(h.Id)).ToListAsync();
        if (rooms.Count!=ids.Count || rooms.Any(h=>h.Estado!="Disponible") ||
            await db.DetallesReserva.AnyAsync(d=>ids.Contains(d.HabitacionId) &&
                d.Reserva.FechaLlegada<model.FechaSalida && d.Reserva.FechaSalida>model.FechaLlegada))
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError("", "Una de las habitaciones ya no está disponible en las fechas indicadas.");
            await CargarClientes(); return View(model);
        }
        var reserva=new Reserva
        {
            ClienteId=model.ClienteId,
            FechaLlegada=model.FechaLlegada, FechaSalida=model.FechaSalida, Dias=dias,
            Detalles=model.Habitaciones.Select(item=>
            {
                var room=rooms.Single(r=>r.Id==item.HabitacionId);
                return new DetalleReserva { HabitacionId=room.Id, NombreHuesped=item.NombreHuesped.Trim(),
                    TarifaDia=room.CostoDia, Dias=dias, Monto=room.CostoDia*dias };
            }).ToList()
        };
        reserva.Subtotal=reserva.Detalles.Sum(x=>x.Monto);
        reserva.Igv=Math.Round(reserva.Subtotal*IgvTasa,2,MidpointRounding.AwayFromZero);
        reserva.Total=reserva.Subtotal+reserva.Igv;
        db.Reservas.Add(reserva);
        foreach(var room in rooms) room.Estado="Reservado";
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Exito"]=$"Reserva generada con el Nro. {reserva.Id}.";
        return RedirectToAction(nameof(Detalle),new {id=reserva.Id});
    }
    public async Task<IActionResult> Detalle(int id)
    {
        var r=await db.Reservas.AsNoTracking().Include(x=>x.Cliente)
            .Include(x=>x.Detalles).ThenInclude(x=>x.Habitacion)
            .FirstOrDefaultAsync(x=>x.Id==id);
        return r==null ? NotFound() : View(r);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var reserva = await db.Reservas
            .AsNoTracking()
            .Include(r => r.Detalles)
            .ThenInclude(d => d.Habitacion)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reserva == null) return NotFound();

        await CargarClientes();

        ViewBag.HabitacionesIniciales = reserva.Detalles
            .Select(d => new
            {
                habitacionId = d.HabitacionId,
                nombreHuesped = d.NombreHuesped,
                numero = d.Habitacion.Numero,
                tipo = d.Habitacion.Tipo,
                costoDia = d.Habitacion.CostoDia
            }).ToList();

        return View(new EditarReservaVm
        {
            Id = reserva.Id,
            ClienteId = reserva.ClienteId,
            FechaLlegada = reserva.FechaLlegada,
            FechaSalida = reserva.FechaSalida,
            Habitaciones = reserva.Detalles.Select(d =>
                new ReservaItemVm
                {
                    HabitacionId = d.HabitacionId,
                    NombreHuesped = d.NombreHuesped
                }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(EditarReservaVm model)
    {
        model.Habitaciones ??= new List<ReservaItemVm>();
        model.FechaLlegada = model.FechaLlegada.Date;
        model.FechaSalida = model.FechaSalida.Date;

        if (model.FechaLlegada < DateTime.Today ||
            model.FechaSalida <= model.FechaLlegada)
        {
            ModelState.AddModelError("",
                "Seleccione fechas válidas.");
        }

        if (model.Habitaciones.Count == 0)
            ModelState.AddModelError("",
                "Agregue al menos una habitación.");

        var ids = model.Habitaciones
            .Select(x => x.HabitacionId).ToList();

        if (ids.Distinct().Count() != ids.Count)
            ModelState.AddModelError("",
                "No puede repetir una habitación.");

        if (!ModelState.IsValid)
            return await MostrarEditar(model);

        if (!await db.Clientes.AnyAsync(
            c => c.Id == model.ClienteId))
        {
            ModelState.AddModelError("", "Cliente no existe.");
            return await MostrarEditar(model);
        }

        await using var tx = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var reserva = await db.Reservas
            .Include(r => r.Detalles)
            .FirstOrDefaultAsync(r => r.Id == model.Id);

        if (reserva == null) return NotFound();

        var idsAnteriores = reserva.Detalles
            .Select(d => d.HabitacionId).ToList();

        var habitaciones = await db.Habitaciones
            .Where(h => ids.Contains(h.Id))
            .ToListAsync();

        bool conflicto = habitaciones.Count != ids.Count
            || habitaciones.Any(h =>
                h.Estado != "Disponible" &&
                !(h.Estado == "Reservado" && idsAnteriores.Contains(h.Id)))
            || await db.DetallesReserva.AnyAsync(d =>
                d.ReservaId != model.Id &&
                ids.Contains(d.HabitacionId) &&
                d.Reserva.FechaLlegada < model.FechaSalida &&
                d.Reserva.FechaSalida > model.FechaLlegada);

        if (conflicto)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError("",
                "Una habitación no está disponible.");
            return await MostrarEditar(model);
        }

        int dias = (model.FechaSalida -
                    model.FechaLlegada).Days;

        db.DetallesReserva.RemoveRange(reserva.Detalles);
        await db.SaveChangesAsync();

        reserva.ClienteId = model.ClienteId;
        reserva.FechaLlegada = model.FechaLlegada;
        reserva.FechaSalida = model.FechaSalida;
        reserva.Dias = dias;

        reserva.Detalles = model.Habitaciones
            .Select(item =>
            {
                var h = habitaciones.Single(
                    x => x.Id == item.HabitacionId);

                return new DetalleReserva
                {
                    HabitacionId = h.Id,
                    NombreHuesped = item.NombreHuesped.Trim(),
                    TarifaDia = h.CostoDia,
                    Dias = dias,
                    Monto = h.CostoDia * dias
                };
            }).ToList();

        reserva.Subtotal = reserva.Detalles
            .Sum(d => d.Monto);

        reserva.Igv = Math.Round(
            reserva.Subtotal * IgvTasa, 2,
            MidpointRounding.AwayFromZero);

        reserva.Total = reserva.Subtotal + reserva.Igv;

        await db.SaveChangesAsync();

        await ActualizarEstados(
            idsAnteriores.Union(ids).ToList());

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Exito"] = "Reserva actualizada correctamente.";

        return RedirectToAction("Detalle",
            new { id = reserva.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        await using var tx = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var reserva = await db.Reservas
            .Include(r => r.Detalles)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reserva == null) return NotFound();

        var ids = reserva.Detalles
            .Select(d => d.HabitacionId).ToList();

        db.Reservas.Remove(reserva);
        await db.SaveChangesAsync();

        await ActualizarEstados(ids);
        await db.SaveChangesAsync();

        await tx.CommitAsync();

        TempData["Exito"] = "Reserva eliminada correctamente.";

        return RedirectToAction("Index");
    }

    private async Task ActualizarEstados(List<int> ids)
    {
        var reservadas = await db.DetallesReserva
            .Where(d => ids.Contains(d.HabitacionId) &&
                        d.Reserva.FechaSalida > DateTime.Today)
            .Select(d => d.HabitacionId)
            .Distinct()
            .ToListAsync();

        var habitaciones = await db.Habitaciones
            .Where(h => ids.Contains(h.Id))
            .ToListAsync();

        foreach (var habitacion in habitaciones)
        {
            if (habitacion.Estado != "Ocupado")
                habitacion.Estado =
                    reservadas.Contains(habitacion.Id)
                    ? "Reservado" : "Disponible";
        }
    }

    private async Task<IActionResult> MostrarEditar(
        EditarReservaVm model)
    {
        await CargarClientes();

        var ids = model.Habitaciones
            .Select(x => x.HabitacionId).ToList();

        var habitaciones = await db.Habitaciones
            .AsNoTracking()
            .Where(h => ids.Contains(h.Id))
            .ToListAsync();

        ViewBag.HabitacionesIniciales =
            model.Habitaciones.Select(item =>
            {
                var h = habitaciones.FirstOrDefault(
                    x => x.Id == item.HabitacionId);

                return new
                {
                    habitacionId = item.HabitacionId,
                    nombreHuesped = item.NombreHuesped,
                    numero = h?.Numero ?? "?",
                    tipo = h?.Tipo ?? "",
                    costoDia = h?.CostoDia ?? 0m
                };
            }).ToList();

        return View("Editar", model);
    }

    private async Task CargarClientes()
    {
        ViewBag.Clientes=new SelectList(await db.Clientes.AsNoTracking()
            .OrderBy(x=>x.Apellidos).Select(x=>new {x.Id,Nombre=x.Nombres+" "+x.Apellidos}).ToListAsync(),"Id","Nombre");
    }
}
