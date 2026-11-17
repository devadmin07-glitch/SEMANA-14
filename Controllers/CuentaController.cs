using System.Security.Claims;
using HotelReservasMVC.Data;
using HotelReservasMVC.Models;
using HotelReservasMVC.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HotelReservasMVC.Controllers;

// Inicio de sesión únicamente. El usuario de demostración se instala desde SQL.
public class CuentaController(SistemaDbContext db) : Controller
{
    private static readonly PasswordHasher<Recepcionista> Hasher = new();

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginVm { Usuario = "Recepcionista", ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("iniciar-sesion")]
    public async Task<IActionResult> Login(LoginVm model)
    {
        if (!ModelState.IsValid) return View(model);

        var usuario = model.Usuario.Trim();
        Recepcionista? cuenta;
        try
        {
            cuenta = await db.Recepcionistas.SingleOrDefaultAsync(x => x.Usuario == usuario && x.Activo);
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "No se pudo conectar con SQL Server. Ejecute el script 04 y revise appsettings.json.");
            return View(model);
        }

        var resultado = cuenta is null
            ? PasswordVerificationResult.Failed
            : Hasher.VerifyHashedPassword(cuenta, cuenta.PasswordHash, model.Contrasena);

        if (cuenta is null || resultado == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
            return View(model);
        }

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            cuenta.PasswordHash = Hasher.HashPassword(cuenta, model.Contrasena);
            await db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, cuenta.Id.ToString()),
            new(ClaimTypes.Name, cuenta.NombreCompleto),
            new(ClaimTypes.Role, "Recepcionista")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            principal, new AuthenticationProperties { IsPersistent = false });

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarSesion()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
