using backend.Data;
using backend.Hubs;          // 👈 UploadHub
using backend.Models;
using CloudinaryDotNet;      // 👈 Cloudinary
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.SignalR;  // 👈 IHubContext
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryResourceType = CloudinaryDotNet.Actions.ResourceType;

public class UploadReconciliationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<UploadReconciliationWorker> _logger;
    private readonly IHubContext<UploadHub> _hubContext;       // 👈 Add this

    public UploadReconciliationWorker(
        IServiceProvider serviceProvider,
        Cloudinary cloudinary,
        ILogger<UploadReconciliationWorker> logger,
        IHubContext<UploadHub> hubContext)                     // 👈 Add this
    {
        _serviceProvider = serviceProvider;
        _cloudinary = cloudinary;
        _logger = logger;
        _hubContext = hubContext;                              // 👈 Add this
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
                    .Take(100)
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

                        // ✅ Notify client — worker recovered their post
                        await _hubContext.Clients
                            .Group($"user-{post.UserId}")
                            .SendAsync("UploadComplete", new
                            {
                                postId = post.Id,
                                mediaUrl = post.MediaUrl,
                                status = "Ready"
                            });

                        _logger.LogInformation("Post {PostId} recovered and client notified.", post.Id);
                    }
                    else if (resourceResult.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        post.Status = UploadStatus.Failed;
                        post.LastUpdatedAt = DateTime.UtcNow;

                        // ✅ Notify client — their upload failed
                        await _hubContext.Clients
                            .Group($"user-{post.UserId}")
                            .SendAsync("UploadFailed", new
                            {
                                postId = post.Id,
                                status = "Failed"
                            });

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