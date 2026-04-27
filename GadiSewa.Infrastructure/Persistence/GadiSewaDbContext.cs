using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using GadiSewa.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.Infrastructure.Persistence;

public sealed class GadiSewaDbContext : DbContext
{
    public GadiSewaDbContext(DbContextOptions<GadiSewaDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Staff> Staffs => Set<Staff>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<PartRequest> PartRequests => Set<PartRequest>();
    public DbSet<CreditPayment> CreditPayments => Set<CreditPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.Property(x => x.PhoneNumber).HasMaxLength(20);
            entity.Property(x => x.PasswordHash).HasMaxLength(1000);
            entity.Property(x => x.Role).HasConversion<int>();

            entity
                .HasOne(x => x.StaffProfile)
                .WithOne(x => x.User)
                .HasForeignKey<Staff>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(x => x.CustomerProfile)
                .WithOne(x => x.User)
                .HasForeignKey<Customer>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasIndex(x => x.EmployeeCode).IsUnique();
            entity.Property(x => x.EmployeeCode).HasMaxLength(50);
            entity.Property(x => x.Position).HasMaxLength(100);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.Address).HasMaxLength(300);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasIndex(x => x.RegistrationNumber).IsUnique();
            entity.Property(x => x.RegistrationNumber).HasMaxLength(30);
            entity.Property(x => x.Make).HasMaxLength(100);
            entity.Property(x => x.Model).HasMaxLength(100);
            entity.Property(x => x.Color).HasMaxLength(50);

            entity
                .HasOne(x => x.Customer)
                .WithMany(x => x.Vehicles)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Part>(entity =>
        {
            entity.HasIndex(x => x.PartNumber).IsUnique();
            entity.Property(x => x.PartNumber).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.ContactPerson).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.PhoneNumber).HasMaxLength(20);
            entity.Property(x => x.Address).HasMaxLength(300);
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.HasIndex(x => x.InvoiceNumber).IsUnique();
            entity.Property(x => x.InvoiceNumber).HasMaxLength(60);
            entity.Property(x => x.SubTotal).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<int>();

            entity
                .HasOne(x => x.Vendor)
                .WithMany(x => x.PurchaseInvoices)
                .HasForeignKey(x => x.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.CreatedByStaff)
                .WithMany(x => x.PurchaseInvoices)
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseInvoiceItem>(entity =>
        {
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);

            entity
                .HasOne(x => x.PurchaseInvoice)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(x => x.Part)
                .WithMany(x => x.PurchaseInvoiceItems)
                .HasForeignKey(x => x.PartId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesInvoice>(entity =>
        {
            entity.HasIndex(x => x.InvoiceNumber).IsUnique();
            entity.Property(x => x.InvoiceNumber).HasMaxLength(60);
            entity.Property(x => x.SubTotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<int>();

            entity
                .HasOne(x => x.Customer)
                .WithMany(x => x.SalesInvoices)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.CreatedByStaff)
                .WithMany(x => x.SalesInvoices)
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.Appointment)
                .WithOne(x => x.SalesInvoice)
                .HasForeignKey<SalesInvoice>(x => x.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SalesInvoiceItem>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);

            entity
                .HasOne(x => x.SalesInvoice)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(x => x.Part)
                .WithMany(x => x.SalesInvoiceItems)
                .HasForeignKey(x => x.PartId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasIndex(x => x.AppointmentNumber).IsUnique();
            entity.Property(x => x.AppointmentNumber).HasMaxLength(60);
            entity.Property(x => x.ProblemDescription).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Status).HasConversion<int>();

            entity
                .HasOne(x => x.Customer)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.Vehicle)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.AssignedStaff)
                .WithMany(x => x.AssignedAppointments)
                .HasForeignKey(x => x.AssignedStaffId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.Property(x => x.Comment).HasMaxLength(1000);

            entity
                .HasOne(x => x.Customer)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.Appointment)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.CustomerId, x.AppointmentId }).IsUnique();
        });

        modelBuilder.Entity<PartRequest>(entity =>
        {
            entity.HasIndex(x => x.RequestNumber).IsUnique();
            entity.Property(x => x.RequestNumber).HasMaxLength(60);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Notes).HasMaxLength(1000);

            entity
                .HasOne(x => x.RequestedByStaff)
                .WithMany(x => x.PartRequests)
                .HasForeignKey(x => x.RequestedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.Part)
                .WithMany(x => x.PartRequests)
                .HasForeignKey(x => x.PartId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(x => x.Vendor)
                .WithMany(x => x.PartRequests)
                .HasForeignKey(x => x.VendorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CreditPayment>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasMaxLength(50);
            entity.Property(x => x.ReferenceNumber).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(500);

            entity
                .HasOne(x => x.SalesInvoice)
                .WithMany(x => x.CreditPayments)
                .HasForeignKey(x => x.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(x => x.Customer)
                .WithMany(x => x.CreditPayments)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
