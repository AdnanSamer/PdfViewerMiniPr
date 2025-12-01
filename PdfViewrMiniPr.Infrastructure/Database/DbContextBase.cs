using Microsoft.EntityFrameworkCore;
using PdfViewrMiniPr.Domain.Entities;

namespace PdfViewrMiniPr.Infrastructure.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowStamp> WorkflowStamps => Set<WorkflowStamp>();
    public DbSet<WorkflowExternalAccess> WorkflowExternalAccesses => Set<WorkflowExternalAccess>();
    public DbSet<Stamp> Stamps => Set<Stamp>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(builder =>
        {
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
            builder.Property(u => u.FullName).IsRequired().HasMaxLength(256);
        });

        modelBuilder.Entity<Workflow>(builder =>
        {
            builder.Property(w => w.Title).IsRequired().HasMaxLength(256);
            builder.Property(w => w.PdfFilePath).IsRequired().HasMaxLength(1024);

            builder.HasOne(w => w.CreatedByUser)
                .WithMany(u => u.InitiatedWorkflows)
                .HasForeignKey(w => w.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(w => w.InternalReviewer)
                .WithMany(u => u.InternalReviews)
                .HasForeignKey(w => w.InternalReviewerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowStamp>(builder =>
        {
            builder.Property(s => s.Label).IsRequired().HasMaxLength(256);
        });

        modelBuilder.Entity<WorkflowExternalAccess>(builder =>
        {
            builder.HasIndex(x => x.Token).IsUnique();
            builder.Property(x => x.Token).IsRequired().HasMaxLength(256);
        });
    }
}

