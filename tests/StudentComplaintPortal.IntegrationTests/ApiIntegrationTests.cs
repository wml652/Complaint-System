using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using StudentComplaintPortal.Data;
using StudentComplaintPortal.Domain.Entities;
using StudentComplaintPortal.Domain.Enums;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace StudentComplaintPortal.IntegrationTests;

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullWorkflow_RegisterLoginCreateComplaintPostMessage_Success()
    {
        // Register a student
        var registerRequest = new
        {
            email = "integrationstudent@test.com",
            password = "Student123!",
            fullName = "Integration Test Student",
            isAdmin = false
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Login
        var loginRequest = new
        {
            email = "integrationstudent@test.com",
            password = "Student123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginContent.GetProperty("token").GetString();
        Assert.NotNull(token);

        // Set JWT token for subsequent requests
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create a complaint
        var createComplaintRequest = new
        {
            title = "Library Issue",
            description = "Books are not available",
            category = "Academic"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/complaints", createComplaintRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var complaintContent = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var complaintId = complaintContent.GetProperty("id").GetInt32();
        Assert.True(complaintId > 0);

        // Post a message to the complaint
        var sendMessageRequest = new
        {
            content = "Please look into this urgently"
        };

        var messageResponse = await _client.PostAsJsonAsync($"/api/v1/complaints/{complaintId}/messages", sendMessageRequest);
        Assert.Equal(HttpStatusCode.Created, messageResponse.StatusCode);

        // Get conversation
        var conversationResponse = await _client.GetAsync($"/api/v1/complaints/{complaintId}/messages");
        Assert.Equal(HttpStatusCode.OK, conversationResponse.StatusCode);

        var messages = await conversationResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(messages.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Authentication_JwtBearer_Success()
    {
        // Seed a test user via UserManager
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        
        var testUser = new AppUser
        {
            UserName = "jwttest@test.com",
            Email = "jwttest@test.com",
            FullName = "JWT Test User",
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var existingUser = await userManager.FindByEmailAsync("jwttest@test.com");
        if (existingUser == null)
        {
            await userManager.CreateAsync(testUser, "Test123!");
        }

        // Login to get JWT token
        var loginRequest = new
        {
            email = "jwttest@test.com",
            password = "Test123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginContent.GetProperty("token").GetString();

        // Use JWT Bearer token
        var clientWithJwt = _factory.CreateClient();
        clientWithJwt.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await clientWithJwt.GetAsync("/api/v1/complaints/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Authentication_Cookie_Success()
    {
        // Seed a test user
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        
        var testUser = new AppUser
        {
            UserName = "cookietest@test.com",
            Email = "cookietest@test.com",
            FullName = "Cookie Test User",
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var existingUser = await userManager.FindByEmailAsync("cookietest@test.com");
        if (existingUser == null)
        {
            await userManager.CreateAsync(testUser, "Test123!");
        }

        // Create a client that handles cookies
        var clientWithCookies = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        // Login (this sets the auth cookie)
        var loginRequest = new
        {
            email = "cookietest@test.com",
            password = "Test123!"
        };

        var loginResponse = await clientWithCookies.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Access protected endpoint using cookie authentication
        var response = await clientWithCookies.GetAsync("/api/v1/complaints/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RoleEnforcement_StudentCannotUpdateStatus_Returns403()
    {
        // Create and login as student
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        
        var student = new AppUser
        {
            UserName = "roletest@test.com",
            Email = "roletest@test.com",
            FullName = "Role Test Student",
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var existingUser = await userManager.FindByEmailAsync("roletest@test.com");
        if (existingUser == null)
        {
            await userManager.CreateAsync(student, "Test123!");
        }

        var loginRequest = new
        {
            email = "roletest@test.com",
            password = "Test123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginContent.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create a complaint
        var createComplaintRequest = new
        {
            title = "Test Complaint",
            description = "Test Description",
            category = "Academic"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/complaints", createComplaintRequest);
        var complaintContent = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var complaintId = complaintContent.GetProperty("id").GetInt32();

        // Try to update status (should fail)
        var updateStatusRequest = new
        {
            status = "InProgress"
        };

        var statusResponse = await _client.PatchAsJsonAsync($"/api/v1/complaints/{complaintId}/status", updateStatusRequest);
        Assert.Equal(HttpStatusCode.Forbidden, statusResponse.StatusCode);
    }

    [Fact]
    public async Task RoleEnforcement_AdminCanUpdateStatus_Returns200()
    {
        // Seed admin and student users
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        
        var admin = new AppUser
        {
            UserName = "adminrole@test.com",
            Email = "adminrole@test.com",
            FullName = "Admin Role Test",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var student = new AppUser
        {
            UserName = "studentrole@test.com",
            Email = "studentrole@test.com",
            FullName = "Student Role Test",
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        if (await userManager.FindByEmailAsync("adminrole@test.com") == null)
        {
            await userManager.CreateAsync(admin, "Admin123!");
        }

        if (await userManager.FindByEmailAsync("studentrole@test.com") == null)
        {
            await userManager.CreateAsync(student, "Student123!");
        }

        // Login as student and create complaint
        var studentLoginRequest = new { email = "studentrole@test.com", password = "Student123!" };
        var studentLoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", studentLoginRequest);
        var studentLoginContent = await studentLoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var studentToken = studentLoginContent.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var createComplaintRequest = new
        {
            title = "Admin Test Complaint",
            description = "For admin to update",
            category = "Administrative"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/complaints", createComplaintRequest);
        var complaintContent = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var complaintId = complaintContent.GetProperty("id").GetInt32();

        // Login as admin
        var adminLoginRequest = new { email = "adminrole@test.com", password = "Admin123!" };
        var adminLoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", adminLoginRequest);
        var adminLoginContent = await adminLoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var adminToken = adminLoginContent.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Update status as admin (should succeed)
        var updateStatusRequest = new { status = "InProgress" };
        var statusResponse = await _client.PatchAsJsonAsync($"/api/v1/complaints/{complaintId}/status", updateStatusRequest);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
    }

    [Fact]
    public async Task OwnershipEnforcement_StudentCannotAccessOtherStudentComplaint_Returns403()
    {
        // Seed two students
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        
        var student1 = new AppUser
        {
            UserName = "student1@test.com",
            Email = "student1@test.com",
            FullName = "Student One",
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var student2 = new AppUser
        {
            UserName = "student2@test.com",
            Email = "student2@test.com",
            FullName = "Student Two",
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        if (await userManager.FindByEmailAsync("student1@test.com") == null)
        {
            await userManager.CreateAsync(student1, "Student123!");
        }

        if (await userManager.FindByEmailAsync("student2@test.com") == null)
        {
            await userManager.CreateAsync(student2, "Student123!");
        }

        // Student 1 creates a complaint
        var student1LoginRequest = new { email = "student1@test.com", password = "Student123!" };
        var student1LoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", student1LoginRequest);
        var student1LoginContent = await student1LoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var student1Token = student1LoginContent.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", student1Token);

        var createComplaintRequest = new
        {
            title = "Student 1 Complaint",
            description = "Private to student 1",
            category = "Academic"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/complaints", createComplaintRequest);
        var complaintContent = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var complaintId = complaintContent.GetProperty("id").GetInt32();

        // Student 2 tries to access Student 1's complaint
        var student2LoginRequest = new { email = "student2@test.com", password = "Student123!" };
        var student2LoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", student2LoginRequest);
        var student2LoginContent = await student2LoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var student2Token = student2LoginContent.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", student2Token);

        var accessResponse = await _client.GetAsync($"/api/v1/complaints/{complaintId}");
        Assert.Equal(HttpStatusCode.Forbidden, accessResponse.StatusCode);
    }
}
