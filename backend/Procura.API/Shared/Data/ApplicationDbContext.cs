using Microsoft.EntityFrameworkCore;
using Procura.API.Shared.Entities;
using Procura.API.Modules.ProcurementRequest.Entities;
using Procura.API.Modules.VendorManagement.Entities;

using VendorEvaluationEntity = Procura.API.Modules.VendorEvaluation.Entities.VendorEvaluation;
using Procura.API.Modules.VendorEvaluation.Entities;

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
        public DbSet<VendorQuote> VendorQuotes { get; set; } = null!;
        public DbSet<VendorEvaluationCriterionScore> VendorEvaluationCriterionScores { get; set; } = null!;
        public DbSet<VendorEvaluationEntity> VendorEvaluations { get; set; } = null!;
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

            modelBuilder.Entity<VendorQuote>(b =>
            {
                b.HasKey(q => q.Id);
    
                b.Property(q => q.VendorName)
                    .HasMaxLength(150)
                    .IsRequired();

                b.Property(q => q.QuotedPrice)
                    .HasPrecision(18, 2);

                b.Property(q => q.ReliabilityRating)
                    .HasPrecision(5, 2);
                    
                b.Property(q => q.Notes)
                    .HasColumnType("text");
            });

            modelBuilder.Entity<VendorEvaluationEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProcurementRequestId).IsRequired();
                entity.Property(e => e.VendorId).IsRequired();
                entity.Property(e => e.Rank).IsRequired();
                entity.Property(e => e.OverallScore).HasPrecision(5, 2).IsRequired();
                entity.Property(e => e.Reasoning).IsRequired();
                entity.Property(e => e.GeneratedByAgent).HasDefaultValue(false).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();

                // Store RiskFlags as JSON text for universal database support
                entity.Property(e => e.RiskFlags)
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v ?? new List<string>(), (System.Text.Json.JsonSerializerOptions?)null),
                        v => string.IsNullOrEmpty(v)
                            ? new List<string>()
                            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                    )
                    .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                        (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()
                    ));

                entity.HasIndex(e => e.ProcurementRequestId);
                entity.HasIndex(e => e.VendorId);
                entity.HasIndex(e => new { e.ProcurementRequestId, e.VendorId });

                entity.HasMany(e => e.CriterionScores)
                    .WithOne(c => c.VendorEvaluation)
                    .HasForeignKey(c => c.VendorEvaluationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<VendorEvaluationCriterionScore>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.CriterionName).HasConversion<string>().IsRequired();
                entity.Property(c => c.Score).HasPrecision(5, 2).IsRequired();
                entity.Property(c => c.Weight).HasPrecision(4, 3).IsRequired();
                entity.HasIndex(c => c.VendorEvaluationId);
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
