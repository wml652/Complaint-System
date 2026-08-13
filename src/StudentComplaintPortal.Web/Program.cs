using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
using StudentComplaintPortal.Web.Serialization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

#region Core Services
// Add services to the container
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    options.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
});

// Phase 3: Add MVC with Views and Blazor Server
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
#endregion

#region Database Configuration
// Configure Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
#endregion

#region Identity Configuration
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
#endregion

#region Authentication Configuration
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

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

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
#endregion

#region Caching and Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
#endregion

#region SignalR and Real-time Communication
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSignalR();
#endregion

#region Security and Authorization
builder.Services.AddSingleton<IAuthorizationPolicyProvider, StudentComplaintPortal.Web.Security.PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, StudentComplaintPortal.Web.Security.PermissionAuthorizationHandler>();
builder.Services.AddScoped<IRoleManagementService, StudentComplaintPortal.Application.Services.RoleManagementService>();
#endregion

#region HTTP Clients
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CookieHandler>();
builder.Services.AddHttpClient("AuthenticatedClient")
    .AddHttpMessageHandler<CookieHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = false
    });
builder.Services.AddHttpClient();
#endregion

#region Application Services
// Register application services
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IFileStorageService, LocalDiskFileStorageService>();
builder.Services.AddScoped<INotificationPushService, SignalRNotificationPushService>();
builder.Services.AddScoped<IConversationService, StudentComplaintPortal.Application.Services.ConversationService>();
builder.Services.AddScoped<IMessageReadTrackingService, MessageReadTrackingService>();
builder.Services.AddScoped<IMessageQuotaService, MessageQuotaService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
#endregion

#region Swagger Configuration
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
#endregion


var app = builder.Build();

#region Database Initialization
// Apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}
#endregion

#region Development Data Seeding
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
        await StudentComplaintPortal.Data.Seeding.PermissionSeeder.SeedAsync(
            services.GetRequiredService<AppDbContext>());

        await StudentComplaintPortal.Data.Seeding.ConversationSeeder.SeedAsync(
           services.GetRequiredService<AppDbContext>());

        await StudentComplaintPortal.Data.Seeding.DbSeeder.SeedDataAsync(
            services.GetRequiredService<AppDbContext>());
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}
#endregion

#region HTTP Pipeline Configuration
// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseWebSockets();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
#endregion

#region Route Configuration
// Map API controllers
app.MapControllers();

// Map SignalR hub
app.MapHub<ChatHub>("/hubs/chat");

// Map Blazor hub
app.MapBlazorHub();

// Map MVC routes with default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");
#endregion

app.Run();

#region Helper Methods
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

    // Seed a dedicated Staff test account (redirects to the Staff Dashboard on login)
    if (await userManager.FindByEmailAsync("staff@test.com") == null)
    {
        var staff = new AppUser
        {
            UserName = "staff@test.com",
            Email = "staff@test.com",
            FullName = "Test Staff",
            Role = UserRole.Staff,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(staff, "Staff123!");
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
                Role = UserRole.Staff,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Staff123!");
        }
    }
}
#endregion

// Make Program class accessible for integration tests
public partial class Program { }
