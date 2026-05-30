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
using CloudinaryResourceType = CloudinaryDotNet.Actions.ResourceType;
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
            _logger.LogInformation("Reconciliation worker online.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var staleThreshold = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(15));

                    var stuckPosts = await dbContext.Posts
                        .Where(p => p.Status == UploadStatus.Uploading && p.CreatedAt < staleThreshold)
                        .Take(100) // Batch limit
                        .ToListAsync(stoppingToken);

                    if (!stuckPosts.Any()) continue;

                    _logger.LogWarning("Detected {Count} records stuck in transit.", stuckPosts.Count);

                    foreach (var post in stuckPosts)
                    {
                        var getResourceParams = new GetResourceParams(post.PublicId)
                        {
                            ResourceType = post.MediaType == "video"
                                ? CloudinaryResourceType.Video
                                : CloudinaryResourceType.Image
                        };

                        var resourceResult = await _cloudinary.GetResourceAsync(getResourceParams);

                        if (resourceResult.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            post.MediaUrl = resourceResult.SecureUrl;
                            post.Status = UploadStatus.Ready;
                            post.LastUpdatedAt = DateTime.UtcNow;
                            _logger.LogInformation("Post {PostId} recovered and finalized.", post.Id);
                        }
                        else if (resourceResult.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            post.Status = UploadStatus.Failed;
                            post.LastUpdatedAt = DateTime.UtcNow;
                            _logger.LogError("Post {PostId} confirmed failed or abandoned.", post.Id);
                        }
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Reconciliation worker shutting down cleanly.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing reconciliation logic.");
                }
            }
        }
    }
}