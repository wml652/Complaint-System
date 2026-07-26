using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StudentComplaintPortal.Application.Services;
using StudentComplaintPortal.Application.Services.FileStorage;
using StudentComplaintPortal.Data;
using StudentComplaintPortal.Data.Repositories;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
using StudentComplaintPortal.Web.Hubs;
using StudentComplaintPortal.Web.Middleware;
using StudentComplaintPortal.Web.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Phase 3: Add MVC with Views and Blazor Server with Increased Message Limits for Voice/Media
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = true;
}).AddHubOptions(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB limit for voice/media streams
    options.EnableDetailedErrors = true;
});

// Configure Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();

// Configure Authentication with Policy Scheme
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "JWT_OR_COOKIE";
    options.DefaultChallengeScheme = "JWT_OR_COOKIE";
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };

    // Phase 2: Support JWT token from query string for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            // Only read token from query string for SignalR hub
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
            {
                context.Token = accessToken;
            }
            
            return Task.CompletedTask;
        }
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
})
.AddPolicyScheme("JWT_OR_COOKIE", "JWT_OR_COOKIE", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
});

builder.Services.AddAuthorization();

builder.Services.AddSingleton<StudentComplaintPortal.Application.Services.MessageBufferService>();

builder.Services.AddHostedService<StudentComplaintPortal.Web.Services.MessageFlushWorker>();

// Phase 2: Add SignalR
builder.Services.AddSignalR();

// Phase 3: Add HttpClient for Blazor components with authentication
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CookieHandler>();
builder.Services.AddHttpClient("AuthenticatedClient")
    .AddHttpMessageHandler<CookieHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = false // We handle cookies manually
    });
builder.Services.AddHttpClient();

// Register application services
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IFileStorageService, LocalDiskFileStorageService>();
builder.Services.AddScoped<INotificationPushService, SignalRNotificationPushService>();

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Student Complaint Portal API",
        Version = "v1",
        Description = "REST API for Student Complaint Portal - Phase 1 & 2"
    });

    // Add JWT authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed test users in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    await SeedTestUsers(userManager);
}

// Seed categories and initial data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await StudentComplaintPortal.Data.Seeding.DbSeeder.SeedDataAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

// Phase 2: Serve static files for uploads
app.UseStaticFiles();

app.UseWebSockets();

app.UseAuthentication();
app.UseAuthorization();

// Map API controllers
app.MapControllers();

// Phase 2: Map SignalR hub
app.MapHub<ChatHub>("/hubs/chat");

// Phase 3: Map Blazor hub
app.MapBlazorHub();

// Map MVC routes with default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();

// Seed test users method
// Seed test users method
static async Task SeedTestUsers(UserManager<AppUser> userManager)
{
    // Seed Student
    if (await userManager.FindByEmailAsync("student@test.com") == null)
    {
        var student = new AppUser
        {
            UserName = "student@test.com",
            Email = "student@test.com",
            FullName = "Test Student",
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(student, "Student123!");
    }

    // Seed Admin
    if (await userManager.FindByEmailAsync("admin@test.com") == null)
    {
        var admin = new AppUser
        {
            UserName = "admin@test.com",
            Email = "admin@test.com",
            FullName = "Test Admin",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(admin, "Admin123!");
    }

    // ADD YOUR TEAM MEMBERS HERE:
    var teamMembers = new[]
    {
        new { Name = "Mahnoor Fatima", Email = "mahnoor@test.com" },
        new { Name = "Muskan", Email = "muskan@test.com" },
        new { Name = "Faizan", Email = "faizan@test.com" },
        new { Name = "Ahmed", Email = "ahmed@test.com" },
        new { Name = "Faraz", Email = "faraz@test.com" },
        new { Name = "Bisma", Email = "bisma@test.com" }
    };

    foreach (var member in teamMembers)
    {
        if (await userManager.FindByEmailAsync(member.Email) == null)
        {
            var user = new AppUser
            {
                UserName = member.Email,
                Email = member.Email,
                FullName = member.Name,
                Role = UserRole.Admin, // Allows them to manage categories and assigned complaints
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };
            // This automatically generates the secure password hash for "Staff123!"
            await userManager.CreateAsync(user, "Staff123!");
        }
    }
}// Make Program class accessible for integration tests
public partial class Program { }
