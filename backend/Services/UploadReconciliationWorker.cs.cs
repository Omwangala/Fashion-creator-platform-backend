using backend.Data;
using backend.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Services
{
    public class UploadReconciliationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<UploadReconciliationWorker> _logger;

        public UploadReconciliationWorker(IServiceProvider serviceProvider, Cloudinary cloudinary, ILogger<UploadReconciliationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _cloudinary = cloudinary;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("State Reconciliation Fail-Safe engine online.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Audit system state logs every 5 minutes
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var staleThreshold = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(15));

                    // Locate items stranded in "Uploading" status for over 15 minutes
                    var stuckPosts = await dbContext.Posts
                        .Where(p => p.Status == UploadStatus.Uploading && p.CreatedAt < staleThreshold)
                        .ToListAsync(stoppingToken);

                    if (!stuckPosts.Any()) continue;

                    _logger.LogWarning($"Detected {stuckPosts.Count} asynchronous records stuck in transit. Syncing with Cloudinary directory...");

                    foreach (var post in stuckPosts)
                    {
                        var getResourceParams = new GetResourceParams(post.PublicId)
                        {
                            ResourceType = post.MediaType == "video" ? ResourceType.Video : ResourceType.Image
                        };

                        var resourceResult = await _cloudinary.GetResourceAsync(getResourceParams);

                        if (resourceResult.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            // Asset completed ingestion successfully but webhook was lost. Restore consistency:
                            post.MediaUrl = resourceResult.SecureUrl;
                            post.Status = UploadStatus.Ready;
                            _logger.LogInformation($"Post {post.Id} recovered and finalized cleanly.");
                        }
                        else if (resourceResult.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            // Upload never finished on cloud side. Mark as Failed.
                            post.Status = UploadStatus.Failed;
                            _logger.LogError($"Post {post.Id} confirmed as failed or abandoned by client device.");
                        }

                        post.LastUpdatedAt = DateTime.UtcNow;
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing automated reconciliation logic loops.");
                }
            }
        }
    }
}