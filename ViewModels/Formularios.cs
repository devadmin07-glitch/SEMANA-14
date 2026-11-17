using System.ComponentModel.DataAnnotations;

namespace HotelReservasMVC.ViewModels;

public class ClienteVm
{
    [Required, StringLength(90)] public string Nombres { get; set; } = "";
    [Required, StringLength(90)] public string Apellidos { get; set; } = "";
}
public class ReservaVm
{
    [Range(1, int.MaxValue, ErrorMessage="Seleccione un cliente.")] public int ClienteId { get; set; }
    [Required, DataType(DataType.Date)] public DateTime FechaLlegada { get; set; } = DateTime.Today;
    [Required, DataType(DataType.Date)] public DateTime FechaSalida { get; set; } = DateTime.Today.AddDays(1);
    [MinLength(1, ErrorMessage="Agregue al menos una habitación.")]
    public List<ReservaItemVm> Habitaciones { get; set; } = new();
}
public class ReservaItemVm
{
    [Range(1, int.MaxValue)] public int HabitacionId { get; set; }
    [Required, StringLength(150)] public string NombreHuesped { get; set; } = "";
}
public class EditarReservaVm : ReservaVm
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }
}

public class LoginVm
{
    [Required(ErrorMessage = "Ingrese su usuario."), StringLength(50)]
    public string Usuario { get; set; } = "";

    [Required(ErrorMessage = "Ingrese su contraseña."), DataType(DataType.Password)]
    public string Contrasena { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
