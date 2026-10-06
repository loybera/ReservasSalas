using Microsoft.EntityFrameworkCore;
using ReservasSalas.Api.Domain;

namespace ReservasSalas.Api.Data;

public sealed class ReservasDbContext(DbContextOptions<ReservasDbContext> options) : DbContext(options)
{
    public DbSet<Sala> Salas => Set<Sala>();

    public DbSet<Reserva> Reservas => Set<Reserva>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // El esquema lo crea Data/Schema.sql; este mapeo solo tiene que coincidir con él.
        modelBuilder.Entity<Sala>(sala =>
        {
            sala.ToTable("salas");
            sala.HasKey(s => s.Id);
            sala.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            sala.Property(s => s.Nombre).HasColumnName("nombre").HasMaxLength(Sala.LargoMaximoNombre);
            sala.Property(s => s.Capacidad).HasColumnName("capacidad");
        });

        modelBuilder.Entity<Reserva>(reserva =>
        {
            reserva.ToTable("reservas");
            reserva.HasKey(r => r.Id);
            reserva.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            reserva.Property(r => r.SalaId).HasColumnName("sala_id");
            reserva.Property(r => r.Inicio).HasColumnName("inicio");
            reserva.Property(r => r.Fin).HasColumnName("fin");
            reserva.Property(r => r.OrganizadorEmail).HasColumnName("organizador_email").HasMaxLength(Email.LargoMaximo);
            reserva.Property(r => r.Asistentes).HasColumnName("asistentes");
            reserva.Property(r => r.CreadaEn).HasColumnName("creada_en");
            reserva.Property(r => r.CanceladaEn).HasColumnName("cancelada_en");
            reserva.Ignore(r => r.EstaCancelada);

            reserva.HasOne<Sala>().WithMany().HasForeignKey(r => r.SalaId);
        });
    }
}
