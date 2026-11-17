using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using HotelReservasMVC.Data;

namespace HotelReservasMVC.Controllers;

public class HomeController(SistemaDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        bool conectado;
        try
        {
            conectado = await db.Database.CanConnectAsync();
            if (conectado)
            {
                // Verificar también el esquema, no solamente que el servidor responda.
                await db.Clientes.AsNoTracking().Take(1).ToListAsync();
                await db.Habitaciones.AsNoTracking().Take(1).ToListAsync();
            }
        }
        catch { conectado = false; }
        ViewBag.BdConectada = conectado;
        return View();
    }
    [AllowAnonymous] public IActionResult Error() => View();
}
