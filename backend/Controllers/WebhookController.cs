using backend.Data;
using backend.Models;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(AppDbContext context, IConfiguration config, ILogger<WebhookController> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        [HttpPost("cloudinary")]
        public async Task<IActionResult> HandleCloudinaryWebhook()
        {
            // ✅ 1. Read raw body for signature verification
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            // ✅ 2. Verify signature before touching anything
            if (!IsValidCloudinarySignature(rawBody))
            {
                _logger.LogWarning("Webhook received with invalid signature. Possible spoofing attempt.");
                return Unauthorized(new { error = "Invalid webhook signature." });
            }

            // ✅ 3. Parse payload
            CloudinaryWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<CloudinaryWebhookPayload>(rawBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Cloudinary webhook payload.");
                return BadRequest(new { error = "Invalid payload format." });
            }

            if (payload == null || string.IsNullOrEmpty(payload.PublicId))
            {
                _logger.LogWarning("Webhook payload missing required fields.");
                return BadRequest(new { error = "Missing required fields." });
            }

            // ✅ 4. Idempotency — reject duplicate webhook events
            var alreadyProcessed = await _context.ProcessedWebhookEvents
                .AnyAsync(e => e.Id == payload.NotificationId);

            if (alreadyProcessed)
            {
                _logger.LogInformation("Duplicate webhook {NotificationId} ignored.", payload.NotificationId);
                return Ok(new { message = "Already processed." });
            }

            // ✅ 5. Only handle upload complete events
            if (payload.NotificationType != "upload")
            {
                _logger.LogInformation("Ignoring webhook event type: {Type}", payload.NotificationType);
                return Ok(new { message = "Event type not handled." });
            }

            // ✅ 6. Find the post by PublicId
            var post = await _context.Posts
                .FirstOrDefaultAsync(p => p.PublicId == payload.PublicId);

            if (post == null)
            {
                _logger.LogWarning("Webhook received for unknown PublicId {PublicId}.", payload.PublicId);
                return Ok(new { message = "Post not found — may have been deleted." });
            }

            // ✅ 7. Update post status
            post.Status = UploadStatus.Ready;
            post.MediaUrl = payload.SecureUrl ?? post.MediaUrl;
            post.LastUpdatedAt = DateTime.UtcNow;

            // ✅ 8. Record event to prevent future duplicates
            _context.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
            {
                Id = payload.NotificationId,
                ProcessedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Post {PostId} marked Ready via webhook.", post.Id);
            return Ok(new { message = "Post status updated." });
        }

        private bool IsValidCloudinarySignature(string rawBody)
        {
            var signature = Request.Headers["X-Cld-Signature"].FirstOrDefault();
            var timestamp = Request.Headers["X-Cld-Timestamp"].FirstOrDefault();

            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
                return false;

            // Reject timestamps older than 15 minutes — replay attack protection
            if (!long.TryParse(timestamp, out long ts))
                return false;

            var requestTime = DateTimeOffset.FromUnixTimeSeconds(ts);
            if (DateTimeOffset.UtcNow - requestTime > TimeSpan.FromMinutes(15))
            {
                _logger.LogWarning("Webhook timestamp too old — possible replay attack.");
                return false;
            }

            var apiSecret = _config["CloudinarySettings:ApiSecret"]
                ?? throw new InvalidOperationException("Cloudinary ApiSecret not configured.");

            // Cloudinary signature = SHA-1(raw_body + timestamp + api_secret)
            var toSign = rawBody + timestamp + apiSecret;
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(toSign));
            var expected = Convert.ToHexString(hash).ToLower();

            return signature == expected;
        }
    }

    // ── Payload shape ──────────────────────────────────────────
    public class CloudinaryWebhookPayload
    {
        public string PublicId { get; set; } = string.Empty;
        public string? SecureUrl { get; set; }
        public string NotificationType { get; set; } = string.Empty;
        public string NotificationId { get; set; } = string.Empty;
    }
}