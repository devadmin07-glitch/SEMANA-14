using Microsoft.EntityFrameworkCore;
using HotelReservasMVC.Models;

namespace HotelReservasMVC.Data;

public class SistemaDbContext(DbContextOptions<SistemaDbContext> options) : DbContext(options)
{
    public DbSet<Recepcionista> Recepcionistas => Set<Recepcionista>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Habitacion> Habitaciones => Set<Habitacion>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<DetalleReserva> DetallesReserva => Set<DetalleReserva>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recepcionista>().HasIndex(x => x.Usuario).IsUnique();
        modelBuilder.Entity<Habitacion>().HasIndex(x => x.Numero).IsUnique();
        modelBuilder.Entity<Habitacion>().Property(x => x.CostoDia).HasPrecision(12, 2);
        modelBuilder.Entity<Reserva>().Property(x => x.Subtotal).HasPrecision(12, 2);
        modelBuilder.Entity<Reserva>().Property(x => x.Igv).HasPrecision(12, 2);
        modelBuilder.Entity<Reserva>().Property(x => x.Total).HasPrecision(12, 2);
        modelBuilder.Entity<DetalleReserva>().Property(x => x.TarifaDia).HasPrecision(12, 2);
        modelBuilder.Entity<DetalleReserva>().Property(x => x.Monto).HasPrecision(12, 2);
        modelBuilder.Entity<Reserva>().HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DetalleReserva>().HasOne(x => x.Habitacion).WithMany().HasForeignKey(x => x.HabitacionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DetalleReserva>().HasOne(x => x.Reserva).WithMany(r => r.Detalles).HasForeignKey(x => x.ReservaId);
        modelBuilder.Entity<DetalleReserva>().HasIndex(x => new { x.ReservaId, x.HabitacionId }).IsUnique();
        modelBuilder.Entity<Reserva>().HasIndex(x => new { x.FechaLlegada, x.FechaSalida });
    }
}
