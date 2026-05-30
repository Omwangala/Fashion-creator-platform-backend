using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Post> Posts { get; set; }
        public DbSet<User> Users { get; set; }

        // ➕ ADDED: Stores unique event IDs to prevent duplicate webhook executions
        public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── USER ──────────────────────────────────────────
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("IX_User_Username");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("IX_User_Email");

            // ── POST ──────────────────────────────────────────
            modelBuilder.Entity<Post>()
                .HasIndex(p => new { p.UserId, p.CreatedAt })
                .HasDatabaseName("IX_Post_UserId_CreatedAt");

            modelBuilder.Entity<Post>()
                .HasIndex(p => p.PublicId)
                .IsUnique()
                .HasDatabaseName("IX_Post_PublicId");

            modelBuilder.Entity<Post>()
                .HasIndex(p => p.Status)
                .HasDatabaseName("IX_Post_Status");

            // ── WEBHOOK ───────────────────────────────────────
            modelBuilder.Entity<ProcessedWebhookEvent>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<ProcessedWebhookEvent>()
                .Property(e => e.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<Post>()
                .HasIndex(p => new { p.Status, p.CreatedAt })
                .HasDatabaseName("IX_Post_Status_CreatedAt");
        }
    }
}