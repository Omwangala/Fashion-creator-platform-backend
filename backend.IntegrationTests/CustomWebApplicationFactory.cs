using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Services;

namespace backend.IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.Testing.json", optional: false, reloadOnChange: false);
            });

            builder.ConfigureServices(services =>
            {
                // ✅ Remove ALL EF-related registrations
                var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
                ).ToList();

                foreach (var d in toRemove)
                    services.Remove(d);

                // ✅ Register in-memory DB
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("SharedTestDb"),
                    ServiceLifetime.Scoped);

                // ✅ Replace Cloudinary with fake
                var imageDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IImageService));
                if (imageDescriptor != null)
                    services.Remove(imageDescriptor);

                services.AddScoped<IImageService, FakeImageService>();
            });
        }

        // ✅ Call this AFTER the host is built to seed the DB
        public void InitializeDb()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        }
    }
}