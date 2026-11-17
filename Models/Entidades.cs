using System.ComponentModel.DataAnnotations;

namespace HotelReservasMVC.Models;

public class Cliente
{
    public int Id { get; set; }
    [Required, MaxLength(90)] public string Nombres { get; set; } = "";
    [Required, MaxLength(90)] public string Apellidos { get; set; } = "";
    public string NombreCompleto => $"{Nombres} {Apellidos}";
}

public class Habitacion
{
    public int Id { get; set; }
    [Required, MaxLength(10)] public string Numero { get; set; } = "";
    [Required, MaxLength(40)] public string Tipo { get; set; } = "";
    [Range(0, 100000)] public decimal CostoDia { get; set; }
    [Required, MaxLength(15)] public string Estado { get; set; } = "Disponible";
}

public class Reserva
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public DateTime FechaLlegada { get; set; }
    public DateTime FechaSalida { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
    public int Dias { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public List<DetalleReserva> Detalles { get; set; } = new();
}

public class DetalleReserva
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public Reserva Reserva { get; set; } = null!;
    public int HabitacionId { get; set; }
    public Habitacion Habitacion { get; set; } = null!;
    [Required, MaxLength(150)] public string NombreHuesped { get; set; } = "";
    public decimal TarifaDia { get; set; }
    public int Dias { get; set; }
    public decimal Monto { get; set; }
}

// Cuenta de la recepcionista autenticada con cookie.
// La contraseña se almacena únicamente como hash PBKDF2 de ASP.NET Core Identity.
public class Recepcionista
{
    public int Id { get; set; }
    [Required, MaxLength(50)] public string Usuario { get; set; } = "";
    [Required, MaxLength(120)] public string NombreCompleto { get; set; } = "";
    [Required, MaxLength(500)] public string PasswordHash { get; set; } = "";
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}
