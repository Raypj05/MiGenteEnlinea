# 🚀 Frontend Migration - Phase 1 START

**Date:** November 10, 2025  
**Phase:** Infrastructure Setup (Week 1)  
**Status:** 🟢 **READY TO BEGIN**  
**Duration:** 3-5 days

---

## 📋 Phase 1 Overview

**Goal:** Setup complete MVC infrastructure for consuming REST API with JWT authentication

**What We'll Build:**

1. API Client Service (HTTP wrapper with JWT injection)
2. Authentication Service (Login, Register, Token management)
3. Base Layouts (Master Pages → Razor Layouts)
4. Session/State Management
5. Error Handling & Logging

---

## ✅ Step 1: Install Required NuGet Packages

Run these commands in `MiGenteEnLinea.Web` project:

```powershell
cd "c:\Users\rpena\OneDrive - Dextra\Desktop\MiGenteEnlinea\MiGenteEnLinea.Clean\src\Presentation\MiGenteEnLinea.Web"

# JWT handling
dotnet add package System.IdentityModel.Tokens.Jwt --version 7.0.3
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.0

# PDF generation (iText 7)
dotnet add package itext7 --version 8.0.5

# Bootstrap & UI
dotnet add package LibMan
libman install bootstrap@5.3.0 -p unpkg -d wwwroot/lib/bootstrap
libman install jquery@3.7.1 -p cdnjs -d wwwroot/lib/jquery
libman install sweetalert2@11.10.0 -p unpkg -d wwwroot/lib/sweetalert2
libman install font-awesome@6.5.0 -p cdnjs -d wwwroot/lib/font-awesome

# FluentValidation (client-side)
dotnet add package FluentValidation.AspNetCore --version 11.3.0
```

---

## ✅ Step 2: Update appsettings.json

Add API configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  
  "ApiSettings": {
    "BaseUrl": "https://localhost:5015",
    "Timeout": 30
  },
  
  "JwtSettings": {
    "SecretKey": "your-secret-key-here-min-32-chars-xxxxxxxxxxxxxxxxxxxxxxxx",
    "Issuer": "MiGenteEnLinea.API",
    "Audience": "MiGenteEnLinea.Web",
    "ExpirationMinutes": 480
  },
  
  "Cardnet": {
    "MerchantId": "349000001",
    "ApiUrl": "https://ecommerce.cardnet.com.do/api/payment/",
    "TimeoutSeconds": 30
  },
  
  "Session": {
    "IdleTimeoutHours": 8,
    "CookieName": ".MiGente.Session"
  }
}
```

---

## ✅ Step 3: Create API Service

**File:** `Services/IApiService.cs`

```csharp
namespace MiGenteEnLinea.Web.Services;

public interface IApiService
{
    Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);
    Task<T?> PostAsync<TRequest, T>(string endpoint, TRequest data, CancellationToken cancellationToken = default);
    Task<T?> PutAsync<TRequest, T>(string endpoint, TRequest data, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string endpoint, CancellationToken cancellationToken = default);
    
    // Specialized methods
    Task<byte[]?> GetBytesAsync(string endpoint, CancellationToken cancellationToken = default);
    Task<Stream?> GetStreamAsync(string endpoint, CancellationToken cancellationToken = default);
}
```

**File:** `Services/ApiService.cs`

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MiGenteEnLinea.Web.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ILogger<ApiService> _logger;
    
    public ApiService(
        HttpClient httpClient,
        IHttpContextAccessor contextAccessor,
        ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _contextAccessor = contextAccessor;
        _logger = logger;
    }
    
    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await LogErrorResponse(endpoint, response);
                return default;
            }
            
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GET {Endpoint}", endpoint);
            throw;
        }
    }
    
    public async Task<T?> PostAsync<TRequest, T>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            
            var response = await _httpClient.PostAsJsonAsync(endpoint, data, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await LogErrorResponse(endpoint, response);
                return default;
            }
            
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling POST {Endpoint}", endpoint);
            throw;
        }
    }
    
    public async Task<T?> PutAsync<TRequest, T>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            
            var response = await _httpClient.PutAsJsonAsync(endpoint, data, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await LogErrorResponse(endpoint, response);
                return default;
            }
            
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling PUT {Endpoint}", endpoint);
            throw;
        }
    }
    
    public async Task<bool> DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            
            var response = await _httpClient.DeleteAsync(endpoint, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await LogErrorResponse(endpoint, response);
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling DELETE {Endpoint}", endpoint);
            throw;
        }
    }
    
    public async Task<byte[]?> GetBytesAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await LogErrorResponse(endpoint, response);
                return null;
            }
            
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GET (bytes) {Endpoint}", endpoint);
            throw;
        }
    }
    
    public async Task<Stream?> GetStreamAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthorizationHeader();
            
            var response = await _httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await LogErrorResponse(endpoint, response);
                return null;
            }
            
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GET (stream) {Endpoint}", endpoint);
            throw;
        }
    }
    
    private void SetAuthorizationHeader()
    {
        var token = GetJwtToken();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }
    }
    
    private string? GetJwtToken()
    {
        return _contextAccessor.HttpContext?.User
            .FindFirst("jwt_token")?.Value;
    }
    
    private async Task LogErrorResponse(string endpoint, HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        _logger.LogWarning(
            "API call failed: {Method} {Endpoint} - Status: {StatusCode}, Response: {Content}",
            response.RequestMessage?.Method,
            endpoint,
            response.StatusCode,
            content);
    }
}
```

---

## ✅ Step 4: Create Authentication Service

**File:** `Services/IAuthService.cs`

```csharp
namespace MiGenteEnLinea.Web.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string email, string password, bool rememberMe);
    Task<RegisterResult> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<bool> RefreshTokenAsync();
    string? GetCurrentUserId();
    string? GetCurrentUserName();
    string? GetCurrentUserRole();
    bool IsAuthenticated();
}

public record LoginResult(bool Success, string? Message, string? Role);
public record RegisterResult(bool Success, string? Message);
public record RegisterRequest(
    string Email,
    string Password,
    string Nombre,
    string Apellido,
    string TipoUsuario);
```

**File:** `Services/AuthService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MiGenteEnLinea.Web.Services;

public class AuthService : IAuthService
{
    private readonly IApiService _apiService;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ILogger<AuthService> _logger;
    
    public AuthService(
        IApiService apiService,
        IHttpContextAccessor contextAccessor,
        ILogger<AuthService> logger)
    {
        _apiService = apiService;
        _contextAccessor = contextAccessor;
        _logger = logger;
    }
    
    public async Task<LoginResult> LoginAsync(string email, string password, bool rememberMe)
    {
        try
        {
            var response = await _apiService.PostAsync<object, LoginResponse>(
                "/api/auth/login",
                new { email, password });
            
            if (response?.Token == null)
            {
                return new LoginResult(false, "Credenciales inválidas", null);
            }
            
            // Parse JWT token
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(response.Token);
            var claims = token.Claims.ToList();
            
            // Add token claim for API calls
            claims.Add(new Claim("jwt_token", response.Token));
            
            // Get role
            var role = claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            
            // Create authentication cookie
            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);
            
            var context = _contextAccessor.HttpContext!;
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
            
            _logger.LogInformation("User logged in: {Email}", email);
            
            return new LoginResult(true, "Login exitoso", role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", email);
            return new LoginResult(false, "Error al iniciar sesión. Intente nuevamente.", null);
        }
    }
    
    public async Task<RegisterResult> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _apiService.PostAsync<RegisterRequest, object>(
                "/api/auth/register",
                request);
            
            if (response != null)
            {
                return new RegisterResult(true, "Registro exitoso. Revise su correo para activar su cuenta.");
            }
            
            return new RegisterResult(false, "Error al registrar usuario");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for {Email}", request.Email);
            return new RegisterResult(false, "Error al registrar. Intente nuevamente.");
        }
    }
    
    public async Task LogoutAsync()
    {
        var context = _contextAccessor.HttpContext!;
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User logged out");
    }
    
    public async Task<bool> RefreshTokenAsync()
    {
        // TODO: Implement token refresh logic
        await Task.CompletedTask;
        return false;
    }
    
    public string? GetCurrentUserId()
    {
        return _contextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
    
    public string? GetCurrentUserName()
    {
        return _contextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Name)?.Value;
    }
    
    public string? GetCurrentUserRole()
    {
        return _contextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Role)?.Value;
    }
    
    public bool IsAuthenticated()
    {
        return _contextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}

public record LoginResponse(string Token, string RefreshToken, int ExpiresIn);
```

---

## ✅ Step 5: Update Program.cs

Replace existing `Program.cs` with complete configuration:

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using MiGenteEnLinea.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Configure session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = builder.Configuration["Session:CookieName"] ?? ".MiGente.Session";
});

// Configure authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// Configure HttpClient for API
builder.Services.AddHttpClient<IApiService, ApiService>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5015";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("ApiSettings:Timeout", 30));
});

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Order: Session → Authentication → Authorization
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
```

---

**¿Quieres que continúe con los siguientes pasos (Layouts, Controllers, Views)?**

Este es un proyecto grande. Podemos hacerlo por fases incrementales.

**Próximos pasos propuestos:**

1. ✅ Completar Phase 1 (Infrastructure)
2. ✅ Crear AuthController + Login/Register views
3. ✅ Migrar primera página de dashboard
4. ✅ Continuar iterativamente

¿Te gustaría que implementemos estos servicios ahora y luego continuemos con los controllers y views?
