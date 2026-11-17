using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelReservasMVC.Data;
using HotelReservasMVC.Models;
using HotelReservasMVC.ViewModels;

namespace HotelReservasMVC.Controllers;

public class ClientesController(SistemaDbContext db) : Controller
{
    [HttpGet] public IActionResult Nuevo() => View(new ClienteVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(ClienteVm model)
    {
        if (!ModelState.IsValid) return View(model);
        var cliente = new Cliente { Nombres=model.Nombres.Trim(), Apellidos=model.Apellidos.Trim() };
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        TempData["Exito"] = $"Cliente registrado con código {cliente.Id}.";
        return RedirectToAction("Nueva","Reservas",new { clienteId=cliente.Id });
    }
    [HttpGet]
    public async Task<IActionResult> Buscar(string? termino)
    {
        var query=db.Clientes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(termino))
            query=query.Where(x=>x.Nombres.Contains(termino)||x.Apellidos.Contains(termino));
        var result=await query.OrderBy(x=>x.Apellidos).Take(30)
            .Select(x=>new { id=x.Id, nombre=x.Nombres+" "+x.Apellidos }).ToListAsync();
        return Json(result);
    }
}
