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

            // ⚡ PERFORMANCE COMPOSITE INDEX
            // Drastically optimizes cursor-based pagination and timelines, preventing expensive table scans
            modelBuilder.Entity<Post>()
                .HasIndex(p => new { p.UserId, p.CreatedAt })
                .HasDatabaseName("IX_Post_UserId_CreatedAt");
        }
    }
}