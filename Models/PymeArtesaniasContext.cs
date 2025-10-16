using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace SistemaComercialPyme.Models;

public partial class PymeArtesaniasContext : DbContext
{
    public PymeArtesaniasContext()
    {
    }

    public PymeArtesaniasContext(DbContextOptions<PymeArtesaniasContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Categoria> Categorias { get; set; }

    public virtual DbSet<Cliente> Clientes { get; set; }

    public virtual DbSet<Compra> Compras { get; set; }

    public virtual DbSet<DetalleCompra> DetalleCompras { get; set; }

    public virtual DbSet<DetalleVenta> DetalleVenta { get; set; }

    public virtual DbSet<MetodoPago> MetodosPago { get; set; }

    public virtual DbSet<Producto> Productos { get; set; }

    public virtual DbSet<Proveedor> Proveedores { get; set; }

    public virtual DbSet<Rol> Roles { get; set; }

    public virtual DbSet<Suscripcion> Suscripciones { get; set; }

    public virtual DbSet<TransaccionStripe> TransaccionesStripe { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    public virtual DbSet<UsuariosCliente> UsuariosClientes { get; set; }

    public virtual DbSet<Venta> Ventas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasKey(e => e.CategoriaId).HasName("PRIMARY");

            entity.ToTable("categorias");

            entity.HasIndex(e => e.Nombre, "Nombre").IsUnique();

            entity.Property(e => e.Nombre).HasMaxLength(50);
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.ClienteId).HasName("PRIMARY");

            entity.ToTable("clientes");

            entity.HasIndex(e => e.Email, "Email").IsUnique();

            entity.Property(e => e.Apellidos).HasMaxLength(100);
            entity.Property(e => e.Direccion).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FechaRegistro)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.NombreComercial).HasMaxLength(100);
            entity.Property(e => e.Nombres).HasMaxLength(100);
            entity.Property(e => e.Telefono).HasMaxLength(20);
            entity.Property(e => e.TipoCliente)
                .HasDefaultValueSql("'Minorista'")
                .HasColumnType("enum('Minorista','Mayorista')");
        });

        modelBuilder.Entity<Compra>(entity =>
        {
            entity.HasKey(e => e.CompraId).HasName("PRIMARY");

            entity.ToTable("compras");

            entity.HasIndex(e => e.MetodoPagoId, "MetodoPagoId");

            entity.HasIndex(e => e.ProveedorId, "ProveedorId");

            entity.Property(e => e.Estado)
                .HasDefaultValueSql("'Pendiente'")
                .HasColumnType("enum('Pendiente','Pagada','Cancelada')");
            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.FechaPago).HasColumnType("datetime");
            entity.Property(e => e.ReferenciaPago).HasMaxLength(100);
            entity.Property(e => e.Total).HasPrecision(10, 2);
            entity.Property(e => e.TotalPagado)
                .HasPrecision(10, 2)
                .HasDefaultValueSql("'0.00'");

            entity.HasOne(d => d.MetodoPago).WithMany(p => p.Compras)
                .HasForeignKey(d => d.MetodoPagoId)
                .HasConstraintName("compras_ibfk_2");

            entity.HasOne(d => d.Proveedor).WithMany(p => p.Compras)
                .HasForeignKey(d => d.ProveedorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("compras_ibfk_1");
        });

        modelBuilder.Entity<DetalleCompra>(entity =>
        {
            entity.HasKey(e => e.DetalleId).HasName("PRIMARY");

            entity.ToTable("detallecompra");

            entity.HasIndex(e => e.CompraId, "CompraId");

            entity.HasIndex(e => e.ProductoId, "ProductoId");

            entity.Property(e => e.PrecioUnitario).HasPrecision(10, 2);
            entity.Property(e => e.Subtotal).HasPrecision(10, 2);

            entity.HasOne(d => d.Compra).WithMany(p => p.DetalleCompras)
                .HasForeignKey(d => d.CompraId)
                .OnDelete(DeleteBehavior.Cascade) //Aquí le modifique para poder eliminar una compra
                .HasConstraintName("detallecompra_ibfk_1");

            entity.HasOne(d => d.Producto).WithMany(p => p.DetalleCompras)
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detallecompra_ibfk_2");
        });

        modelBuilder.Entity<DetalleVenta>(entity =>
        {
            entity.HasKey(e => e.DetalleId);
            entity.ToTable("detalleventa");
            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(10,2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(10,2)");

            entity.HasOne(d => d.Venta)
                  .WithMany(v => v.Detalleventa)
                  .HasForeignKey(d => d.VentaId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DetalleVenta_Venta");

            entity.HasOne(d => d.Producto)
                  .WithMany(p => p.Detalleventa)
                  .HasForeignKey(d => d.ProductoId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_DetalleVenta_Producto");
        });


        modelBuilder.Entity<MetodoPago>(entity =>
        {
            entity.HasKey(e => e.MetodoPagoId).HasName("PRIMARY");

            entity.ToTable("metodospago");

            entity.Property(e => e.Activo).HasDefaultValueSql("'1'");
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.Nombre).HasMaxLength(50);
            entity.Property(e => e.RequiereReferencia).HasDefaultValueSql("'0'");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.ProductoId).HasName("PRIMARY");

            entity.ToTable("productos");

            entity.HasIndex(e => e.CategoriaId, "CategoriaId");

            entity.HasIndex(e => e.Codigo, "Codigo").IsUnique();

            entity.Property(e => e.Codigo).HasMaxLength(50);
            entity.Property(e => e.Costo).HasPrecision(10, 2);
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.Imagen).HasColumnType("text");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.PrecioVenta).HasPrecision(10, 2);
            entity.Property(e => e.StockMinimo).HasDefaultValueSql("'0'");

            entity.HasOne(d => d.Categoria).WithMany(p => p.Productos)
                .HasForeignKey(d => d.CategoriaId)
                .HasConstraintName("productos_ibfk_1");
        });

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.HasKey(e => e.ProveedorId).HasName("PRIMARY");

            entity.ToTable("proveedores");

            entity.Property(e => e.Activo).HasDefaultValueSql("'1'");
            entity.Property(e => e.Contacto).HasMaxLength(100);
            entity.Property(e => e.Direccion).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.Rfc)
                .HasMaxLength(20)
                .HasColumnName("RFC");
            entity.Property(e => e.Telefono).HasMaxLength(20);
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.HasKey(e => e.RolId).HasName("PRIMARY");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Nombre, "Nombre").IsUnique();

            entity.Property(e => e.Nombre).HasMaxLength(50);
        });

        modelBuilder.Entity<Suscripcion>(entity =>
        {
            entity.HasKey(e => e.SuscripcionId).HasName("PRIMARY");

            entity.ToTable("suscripciones");

            entity.HasIndex(e => e.Email, "Email").IsUnique();

            entity.Property(e => e.FechaRegistro)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<TransaccionStripe>(entity =>
        {
            entity.HasKey(e => e.TransaccionId).HasName("PRIMARY");

            entity.ToTable("transaccionesstripe");

            entity.HasIndex(e => e.VentaId, "VentaId");

            entity.Property(e => e.ClientSecret).HasMaxLength(200);
            entity.Property(e => e.FechaActualizacion).HasColumnType("datetime");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.Moneda)
                .HasMaxLength(3)
                .HasDefaultValueSql("'MXN'");
            entity.Property(e => e.Monto).HasPrecision(10, 2);
            entity.Property(e => e.PaymentIntentId).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(d => d.Venta).WithMany(p => p.Transaccionesstripes)
                .HasForeignKey(d => d.VentaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("transaccionesstripe_ibfk_1");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.UsuarioId).HasName("PRIMARY");

            entity.ToTable("usuarios");

            entity.HasIndex(e => e.NombreUsuario, "NombreUsuario").IsUnique();

            entity.HasIndex(e => e.RolId, "RolId");

            entity.Property(e => e.Activo).HasDefaultValueSql("'1'");
            entity.Property(e => e.NombreCompleto).HasMaxLength(100);
            entity.Property(e => e.NombreUsuario).HasMaxLength(50);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);

            entity.HasOne(d => d.Rol).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.RolId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("usuarios_ibfk_1");
        });

        modelBuilder.Entity<UsuariosCliente>(entity =>
        {
            entity.HasKey(e => e.UsuarioId);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(e => e.Email)
                .IsUnique();

            entity.Property(e => e.Nombres).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Apellidos).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FechaRegistro).HasDefaultValueSql("GETDATE()");
        });

        modelBuilder.Entity<Venta>(entity =>
        {
            entity.HasKey(e => e.VentaId);
            entity.ToTable("ventas");
            entity.Property(e => e.Total).HasColumnType("decimal(10,2)");
            entity.Property(e => e.TotalPagado).HasColumnType("decimal(10,2)");
            entity.Property(e => e.ReferenciaPago).HasMaxLength(100);
            entity.Property(e => e.StripePaymentIntentId).HasMaxLength(100);
            entity.Property(e => e.StripeSessionId).HasMaxLength(150);

            entity.HasOne(d => d.Cliente)
                  .WithMany(p => p.Venta)
                  .HasForeignKey(d => d.ClienteId)
                  .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.MetodoPago)
                  .WithMany(p => p.Venta)
                  .HasForeignKey(d => d.MetodoPagoId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_Ventas_MetodoPago");

            entity.HasOne(d => d.Usuario)
                  .WithMany(u => u.Ventas)
                  .HasForeignKey(d => d.UsuarioId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_Ventas_Usuarios");

            entity.HasMany(v => v.Transaccionesstripes)
                  .WithOne(t => t.Venta)
                  .HasForeignKey(t => t.VentaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
