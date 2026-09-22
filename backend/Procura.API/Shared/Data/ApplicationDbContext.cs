using Microsoft.EntityFrameworkCore;
using Procura.API.Shared.Entities;
using Procura.API.Modules.ProcurementRequest.Entities;
using Procura.API.Modules.VendorManagement.Entities;
using Procura.API.AI.Entities;

namespace Procura.API.Shared.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ProcurementRequest> ProcurementRequests { get; set; } = null!;
        public DbSet<ProcurementRequestItem> ProcurementRequestItems { get; set; } = null!;
        public DbSet<WorkflowInstance> WorkflowInstances { get; set; } = null!;
        public DbSet<Vendor> Vendors { get; set; } = null!;
        public DbSet<VendorSelection> VendorSelections { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Role).HasConversion<string>().IsRequired();
            });

                modelBuilder.Entity<VendorSelection>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProcurementRequestId);
                entity.HasIndex(e => e.VendorId);
            });

            modelBuilder.Entity<ProcurementRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.RequestNumber).IsUnique();
                entity.Property(e => e.RequestNumber).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Description).IsRequired();
                entity.Property(e => e.Justification).IsRequired();
                entity.Property(e => e.Priority).HasConversion<string>().IsRequired();
                entity.Property(e => e.Status).HasConversion<string>().IsRequired();
                entity.Property(e => e.RequiredByDate).HasColumnType("date");

                entity.HasOne(e => e.Requester)
                      .WithMany()
                      .HasForeignKey(e => e.RequesterId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Items)
                      .WithOne(e => e.ProcurementRequest)
                      .HasForeignKey(e => e.ProcurementRequestId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProcurementRequestItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ItemName).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Description).IsRequired();
                entity.Property(e => e.Unit).IsRequired().HasMaxLength(30);
            });

            modelBuilder.Entity<Vendor>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.ContactPerson).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Category).IsRequired().HasMaxLength(60);
                entity.Property(e => e.Status).HasConversion<string>().IsRequired();
            });

            modelBuilder.Entity<WorkflowInstance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.RequesterId);
                entity.HasIndex(e => e.ProcurementRequestId);
                entity.Property(e => e.Objective).IsRequired();
                entity.Property(e => e.CurrentStage).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.RequesterRole).IsRequired().HasMaxLength(50);
            });
        }
    }
}
