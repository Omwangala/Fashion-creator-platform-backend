using backend.Data;
using backend.Services;
using CloudinaryDotNet;
using backend.Config;
using backend.Hubs;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;

//  Bootstrap Serilog immediately — catches startup crashes too
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("VogueVault backend starting up...");

    var builder = WebApplication.CreateBuilder(args);

    // ✅ 2. Replace default .NET logging with Serilog
    builder.Host.UseSerilog((ctx, config) =>
        config.WriteTo.Console()
              .ReadFrom.Configuration(ctx.Configuration));

    // Database
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Cloudinary
    builder.Services.Configure<CloudinarySettings>(
        builder.Configuration.GetSection("CloudinarySettings"));

    builder.Services.AddSingleton(provider =>
    {
        var config = provider.GetRequiredService<IOptions<CloudinarySettings>>().Value;
        return new Cloudinary(new Account(
            config.CloudName,
            config.ApiKey,
            config.ApiSecret
        ));
    });

    //  Rate Limiting — LoginPolicy + GeneralPolicy with custom rejection response
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("LoginPolicy", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 5;
            opt.QueueLimit = 0;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        options.AddFixedWindowLimiter("GeneralPolicy", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 60;
            opt.QueueLimit = 0;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        //  Custom rejection response instead of empty 429
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { error = "Too many requests. Please slow down." }, token);
        };
    });

    // JWT — null-safe, fails fast at startup
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("JWT key is not configured. Add 'Jwt:Key' to appsettings.");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // HTTP requests — read from cookie
                var cookieToken = context.Request.Cookies["vault_session"];

                // SignalR connections — read from query string
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                else
                    context.Token = cookieToken;

                return Task.CompletedTask;
            }
        };
    });

    // Services
    builder.Services.AddScoped<IImageService, ImageService>();
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IPostService, PostService>();
    builder.Services.AddHostedService<UploadReconciliationWorker>();
    builder.Services.AddSignalR();
    builder.Services.AddControllers();

    //  Health Checks — pings DB to confirm real connectivity
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opt =>
    {
        opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token"
        });

        opt.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] {}
            }
        });
    });

    // CORS
    var frontendUrl = builder.Configuration["Frontend:Url"]
        ?? throw new InvalidOperationException("Frontend URL is not configured. Add 'Frontend:Url' to appsettings.");

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("VogueVaultPolicy", policy =>
        {
            policy.WithOrigins(frontendUrl)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    var app = builder.Build();

    //  Global Exception Handler — MUST be first in pipeline
    app.UseExceptionHandler(appError =>
    {
        appError.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "An unexpected error occurred. Please try again later."
            });
        });
    });

    app.UseCors("VogueVaultPolicy");

    //  Serilog HTTP request logging
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Apply GeneralPolicy to ALL controller endpoints globally
    app.MapControllers().RequireRateLimiting("GeneralPolicy");
    app.MapHub<UploadHub>("/hubs/upload");

    //  Health check endpoint
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    //  Catches fatal startup crashes and logs them clearly
    Log.Fatal(ex, "VogueVault backend terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}