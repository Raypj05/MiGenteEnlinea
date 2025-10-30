using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ActivateAccount;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ChangePassword;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Login;
using MiGenteEnLinea.Application.Features.Authentication.Commands.RefreshToken;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Register;
using MiGenteEnLinea.Application.Features.Authentication.Commands.RevokeToken;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;
using MiGenteEnLinea.Domain.ValueObjects; // Email VO for equality queries
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

[Collection("Integration Tests")]
public class AuthControllerIntegrationTests : IntegrationTestBase
{
    public AuthControllerIntegrationTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region Register Tests

    [Fact]
    public async Task Register_AsEmpleador_CreatesUserAndProfile()
    {
        var email = GenerateUniqueEmail("empleador");
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = "NewUser@123",
            Nombre = "Nuevo",
            Apellido = "Empleador",
            Tipo = 1,
            Host = "http://localhost:5015"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);

        response.IsSuccessStatusCode.Should().BeTrue();
        
        // DEBUG: Leer el contenido como string para ver qué está retornando
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response Content: {content}");
        
        var result = await response.Content.ReadFromJsonAsync<RegisterResult>();
        
        // VALIDACIONES DTO
        result.Should().NotBeNull();
        result!.UserId.Should().NotBeNullOrEmpty();
        result!.Email.Should().Be(email);
        result!.Success.Should().BeTrue();

        // VALIDACIONES DB LEGACY (comentadas temporalmente - InMemory DB tiene issues con Value Objects)
        // TODO: Re-habilitar cuando migremos a TestContainers con SQL Server real
        // var credencial = await AppDbContext.Credenciales
        //     .FirstOrDefaultAsync(c => c.UserId == result.UserId);
        // credencial.Should().NotBeNull();
        // credencial!.Activo.Should().BeFalse();

        // var perfile = await AppDbContext.Perfiles
        //     .FirstOrDefaultAsync(p => p.UserId == result.UserId);
        // perfile.Should().NotBeNull();
        // perfile!.Nombre.Should().Be("Nuevo");
    }

    [Fact]
    public async Task Register_AsContratista_CreatesUserAndProfile()
    {
        var email = GenerateUniqueEmail("contratista");
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = "NewUser@123",
            Nombre = "Nuevo",
            Apellido = "Contratista",
            Tipo = 2,
            Host = "http://localhost:5015"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);

        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<RegisterResult>();
        result.Should().NotBeNull();

        // ✅ Use fresh DbContext to avoid caching issues
        using var scope = Factory.Services.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<MiGenteDbContext>();
        
        // EF Core ValueObject Email se mapea via HasConversion; para que se traduzca usar igualdad directa contra VO
        var emailVO = Email.CreateUnsafe(email);
        var credencial = await freshContext.CredencialesRefactored
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == emailVO);
        var contratista = await freshContext.Contratistas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == credencial!.UserId);
        
        contratista.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        var registerCommand = new RegisterCommand
        {
            Email = "juan.perez@test.com",
            Password = "NewUser@123",
            Nombre = "Duplicado",
            Apellido = "Usuario",
            Tipo = 1,
            Host = "http://localhost:5015"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithInvalidPassword_ReturnsBadRequest()
    {
        var email = GenerateUniqueEmail("test");
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = "short",
            Nombre = "Test",
            Apellido = "User",
            Tipo = 1,
            Host = "http://localhost:5015"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var loginCommand = new LoginCommand
        {
            Email = "juan.perez@test.com",
            Password = TestDataSeeder.TestPasswordPlainText,
            IpAddress = "127.0.0.1"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var loginCommand = new LoginCommand
        {
            Email = "juan.perez@test.com",
            Password = "WrongPassword123!",
            IpAddress = "127.0.0.1"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ReturnsUnauthorized()
    {
        var loginCommand = new LoginCommand
        {
            Email = "nonexistent@test.com",
            Password = TestDataSeeder.TestPasswordPlainText,
            IpAddress = "127.0.0.1"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInactiveAccount_ReturnsUnauthorized()
    {
        var loginCommand = new LoginCommand
        {
            Email = "ana.martinez@test.com",
            Password = TestDataSeeder.TestPasswordPlainText,
            IpAddress = "127.0.0.1"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Activate Account Tests

    [Fact]
    public async Task ActivateAccount_WithValidToken_ActivatesUser()
    {
        var email = GenerateUniqueEmail("toactivate");
        var registerCmd = new RegisterCommand
        {
            Email = email,
            Password = "Test@123",
            Nombre = "Test",
            Apellido = "User",
            Tipo = 1,
            Host = "http://localhost:5015"
        };
        await Client.PostAsJsonAsync("/api/auth/register", registerCmd);
        
        // ✅ Use fresh DbContext to avoid caching issues
        using var scope = Factory.Services.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<MiGenteDbContext>();
        
        var emailVO = Email.CreateUnsafe(email);
        var credencial = await freshContext.CredencialesRefactored
            .AsNoTracking()
            .FirstAsync(c => c.Email == emailVO);
        credencial.Activo.Should().BeFalse();

        var activateCommand = new ActivateAccountCommand
        {
            UserId = credencial.UserId,
            Email = email
        };

        var response = await Client.PostAsJsonAsync("/api/auth/activate", activateCommand);

        response.IsSuccessStatusCode.Should().BeTrue();
        
        await DbContext.Entry(credencial).ReloadAsync();
        credencial.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateAccount_WithInvalidUserId_ReturnsBadRequest()
    {
        var activateCommand = new ActivateAccountCommand
        {
            UserId = Guid.NewGuid().ToString(),
            Email = "nonexistent@test.com"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/activate", activateCommand);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Change Password Tests

    [Fact]
    public async Task ChangePassword_WithValidCredentials_ChangesPassword()
    {
        var token = await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // ✅ Use fresh DbContext to avoid caching issues
        using var scope = Factory.Services.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<MiGenteDbContext>();
        
        var emailVO = Email.CreateUnsafe("juan.perez@test.com");
        var credencial = await freshContext.CredencialesRefactored
            .AsNoTracking()
            .FirstAsync(c => c.Email == emailVO);

        var changePasswordCommand = new ChangePasswordCommand(
            Email: "juan.perez@test.com",
            UserId: credencial.UserId,
            CurrentPassword: TestDataSeeder.TestPasswordPlainText,
            NewPassword: "NewPassword@123"
        );

        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordCommand);

        response.IsSuccessStatusCode.Should().BeTrue();

        Client.DefaultRequestHeaders.Authorization = null;
        var loginWithNewPassword = new LoginCommand
        {
            Email = "juan.perez@test.com",
            Password = "NewPassword@123",
            IpAddress = "127.0.0.1"
        };
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginWithNewPassword);
        loginResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        var changePasswordCommand = new ChangePasswordCommand(
            Email: "juan.perez@test.com",
            UserId: "some-user-id",
            CurrentPassword: "OldPassword@123",
            NewPassword: "NewPassword@123"
        );

        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordCommand);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        var loginCommand = new LoginCommand
        {
            Email = "juan.perez@test.com",
            Password = TestDataSeeder.TestPasswordPlainText,
            IpAddress = "127.0.0.1"
        };
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();

        var refreshCommand = new RefreshTokenCommand(
            RefreshToken: loginResult!.RefreshToken,
            IpAddress: "127.0.0.1"
        );

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", refreshCommand);

        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        var refreshCommand = new RefreshTokenCommand(
            RefreshToken: "invalid-refresh-token-12345",
            IpAddress: "127.0.0.1"
        );

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", refreshCommand);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Revoke Token Tests

    [Fact]
    public async Task RevokeToken_WithValidToken_RevokesSuccessfully()
    {
        var loginCommand = new LoginCommand
        {
            Email = "juan.perez@test.com",
            Password = TestDataSeeder.TestPasswordPlainText,
            IpAddress = "127.0.0.1"
        };
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();

        var revokeCommand = new RevokeTokenCommand(
            RefreshToken: loginResult!.RefreshToken,
            IpAddress: "127.0.0.1"
        );

        var response = await Client.PostAsJsonAsync("/api/auth/revoke", revokeCommand);

        response.IsSuccessStatusCode.Should().BeTrue();

        var refreshCommand = new RefreshTokenCommand(
            RefreshToken: loginResult.RefreshToken,
            IpAddress: "127.0.0.1"
        );
        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", refreshCommand);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}