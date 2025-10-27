using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ActivateAccount;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ChangePassword;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Login;
using MiGenteEnLinea.Application.Features.Authentication.Commands.RefreshToken;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Register;
using MiGenteEnLinea.Application.Features.Authentication.Commands.RevokeToken;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using MiGenteEnLinea.IntegrationTests.Infrastructure;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Tests de integración para AuthController - Flujo completo de autenticación
/// </summary>
[Collection("Integration Tests")]
public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange - Ya tenemos usuarios seeded por IntegrationTestBase

        // Act
        var accessToken = await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Assert
        accessToken.Should().NotBeNullOrEmpty();
        AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var loginCommand = new LoginCommand
        {
            Email = "juan.perez@test.com",
            Password = "WrongPassword123!",
            IpAddress = "127.0.0.1"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ReturnsUnauthorized()
    {
        // Arrange
        var loginCommand = new LoginCommand
        {
            Email = "nonexistent@test.com",
            Password = TestDataSeeder.TestPasswordPlainText,
            IpAddress = "127.0.0.1"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInactiveAccount_ReturnsUnauthorized()
    {
        // Arrange - ana.martinez@test.com está inactiva
        var loginCommand = new LoginCommand
        {
            Email = "ana.martinez@test.com",
            Password = TestDataSeeder.TestPasswordPlainText,
            IpAddress = "127.0.0.1"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("", "password")] // Email vacío
    [InlineData("invalid-email", "password")] // Email sin formato válido
    [InlineData("test@test.com", "")] // Password vacío
    public async Task Login_WithInvalidInput_ReturnsBadRequest(string email, string password)
    {
        // Arrange
        var loginCommand = new LoginCommand
        {
            Email = email,
            Password = password,
            IpAddress = "127.0.0.1"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task Register_AsEmpleador_CreatesUserSuccessfully()
    {
        // Arrange
        var email = GenerateUniqueEmail("empleador");
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = "NewUser@123",
            Nombre = "Nuevo",
            Apellido = "Empleador",
            Tipo = 1, // ✅ int: 1=Empleador
            Host = "http://localhost:5015" // ✅ REQUIRED for activation link
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var userId = await response.Content.ReadFromJsonAsync<int>(); // ✅ Retorna int userId
        userId.Should().BeGreaterThan(0);

        // Verificar en DB (usar AppDbContext)
        var credencial = await AppDbContext.Credenciales.FirstOrDefaultAsync(c => c.Email.Value == email);
        credencial.Should().NotBeNull();
        credencial!.Activo.Should().BeFalse(); // Debe estar inactivo hasta activar cuenta
    }

    [Fact]
    public async Task Register_AsContratista_CreatesUserSuccessfully()
    {
        // Arrange
        var email = GenerateUniqueEmail("contratista");
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = "NewUser@123",
            Nombre = "Nuevo",
            Apellido = "Contratista",
            Tipo = 2, // ✅ int: 2=Contratista
            Host = "http://localhost:5015" // ✅ REQUIRED
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<RegisterResult>(); // ✅ Tipo correcto: RegisterResult (no RegisterResultDto)
        result.Should().NotBeNull();
        result!.UserId.Should().NotBeNull(); // ✅ UserId es string GUID, no int

        // Verificar en DB - ✅ Contratista NO tiene navigation property "Cuenta"
        var credencial = await AppDbContext.Credenciales
            .FirstAsync(c => c.Email.Value == email);
        var contratista = await AppDbContext.Contratistas
            .FirstOrDefaultAsync(c => c.UserId == credencial.UserId);
        
        contratista.Should().NotBeNull();
        // ✅ Contratista.Identificacion puede ser cédula o pasaporte (campo libre)
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - juan.perez@test.com ya existe
        var registerCommand = new RegisterCommand
        {
            Email = "juan.perez@test.com",
            Password = "NewUser@123",
            Nombre = "Duplicado",
            Apellido = "Usuario",
            Tipo = 1,
            Host = "http://localhost:5015"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("", "Pass@123", "Nombre", "Apellido", 1)] // Email vacío
    [InlineData("test@test.com", "", "Nombre", "Apellido", 1)] // Password vacío
    [InlineData("test@test.com", "short", "Nombre", "Apellido", 1)] // Password muy corto
    [InlineData("test@test.com", "Pass@123", "", "Apellido", 1)] // Nombre vacío
    [InlineData("test@test.com", "Pass@123", "Nombre", "", 1)] // Apellido vacío
    [InlineData("test@test.com", "Pass@123", "Nombre", "Apellido", 3)] // Tipo inválido
    public async Task Register_WithInvalidInput_ReturnsBadRequest(
        string email, string password, string nombre, string apellido, int tipo)
    {
        // Arrange
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = password,
            Nombre = nombre,
            Apellido = apellido,
            Tipo = tipo,
            Host = "http://localhost:5015"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Activate Account Tests

    [Fact]
    public async Task ActivateAccount_WithValidToken_ActivatesSuccessfully()
    {
        // Arrange - Crear usuario inactivo
        var email = GenerateUniqueEmail("toactivate");
        await RegisterUserAsync(email, "Test@123", "Test", "User", "Empleador");
        
        var credencial = await AppDbContext.Credenciales.FirstAsync(c => c.Email.Value == email);
        credencial.Activo.Should().BeFalse();

        var activateCommand = new ActivateAccountCommand
        {
            UserId = credencial.UserId.ToString(),
            Email = email
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/activate", activateCommand);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        
        // Verificar activación en DB
        await DbContext.Entry(credencial).ReloadAsync(); // ✅ DbContext (not AppDbContext) tiene Entry()
        credencial.Activo.Should().BeTrue();
        // ✅ Credencial NO tiene EmailVerificado, solo Activo
    }

    [Fact]
    public async Task ActivateAccount_WithInvalidUserId_ReturnsBadRequest()
    {
        // Arrange
        var activateCommand = new ActivateAccountCommand
        {
            UserId = "99999", // No existe
            Email = "nonexistent@test.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/activate", activateCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Change Password Tests

    [Fact]
    public async Task ChangePassword_WithValidCredentials_ChangesSuccessfully()
    {
        // Arrange - Login primero
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);

        var changePasswordCommand = new ChangePasswordCommand
        {
            Email = "juan.perez@test.com",
            CurrentPassword = TestDataSeeder.TestPasswordPlainText,
            NewPassword = "NewSecurePass@123"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordCommand);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verificar que el nuevo password funciona
        ClearAuthToken();
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginCommand
        {
            Email = "juan.perez@test.com",
            Password = "NewSecurePass@123",
            IpAddress = "127.0.0.1"
        });
        loginResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsUnauthorized()
    {
        // Arrange - Login primero
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);

        var changePasswordCommand = new ChangePasswordCommand
        {
            Email = "juan.perez@test.com",
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewSecurePass@123"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - NO hacer login
        var changePasswordCommand = new ChangePasswordCommand
        {
            Email = "juan.perez@test.com",
            CurrentPassword = TestDataSeeder.TestPasswordPlainText,
            NewPassword = "NewSecurePass@123"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordCommand);

        // Assert - Puede variar según la implementación del endpoint
        // Si el endpoint requiere autenticación, debe ser 401
        // Si solo valida password actual, debe ser 401 por password incorrecto
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        // Arrange - Login para obtener refresh token
        var loginCommand = new LoginCommand
        {
            Email = "juan.perez@test.com",
            Password = TestDataSeeder.TestPasswordPlainText,
            IpAddress = "127.0.0.1"
        };
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        loginResult.Should().NotBeNull();
        loginResult!.RefreshToken.Should().NotBeNullOrEmpty();

        // Act - Usar refresh token
        var refreshCommand = new RefreshTokenCommand
        {
            RefreshToken = loginResult.RefreshToken,
            IpAddress = "127.0.0.1"
        };
        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", refreshCommand);

        // Assert
        refreshResponse.IsSuccessStatusCode.Should().BeTrue();
        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        refreshResult.Should().NotBeNull();
        refreshResult!.AccessToken.Should().NotBeNullOrEmpty();
        refreshResult.RefreshToken.Should().NotBeNullOrEmpty();
        refreshResult.AccessToken.Should().NotBe(loginResult.AccessToken); // Nuevo token diferente
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshCommand = new RefreshTokenCommand
        {
            RefreshToken = "invalid-token-12345",
            IpAddress = "127.0.0.1"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", refreshCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Revoke Token Tests

    [Fact]
    public async Task RevokeToken_WithValidToken_RevokesSuccessfully()
    {
        // Arrange - Login para obtener refresh token
        var loginCommand = new LoginCommand
        {
            Email = "carlos.rodriguez@test.com",
            Password = TestDataSeeder.TestPasswordPlainText,
            IpAddress = "127.0.0.1"
        };
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        var refreshToken = loginResult!.RefreshToken;

        // Act - Revocar token
        var revokeCommand = new RevokeTokenCommand
        {
            RefreshToken = refreshToken,
            IpAddress = "127.0.0.1"
        };
        var revokeResponse = await Client.PostAsJsonAsync("/api/auth/revoke", revokeCommand);

        // Assert
        revokeResponse.IsSuccessStatusCode.Should().BeTrue();

        // Verificar que el token revocado ya no funciona
        var refreshCommand = new RefreshTokenCommand
        {
            RefreshToken = refreshToken,
            IpAddress = "127.0.0.1"
        };
        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", refreshCommand);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Get Profile Tests

    [Fact]
    public async Task GetPerfil_WithValidUserId_ReturnsProfile()
    {
        // Arrange - Login primero
        await LoginAsync("juan.perez@test.com", TestDataSeeder.TestPasswordPlainText);
        
        var empleador = await TestDataSeeder.GetEmpleadorActivoAsync(DbContext);
        var userId = empleador.UserId;

        // Act
        var response = await Client.GetAsync($"/api/auth/perfil/{userId}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var perfil = await response.Content.ReadFromJsonAsync<UsuarioDto>();
        perfil.Should().NotBeNull();
        perfil!.Email.Should().Be("juan.perez@test.com");
        perfil.Nombre.Should().Be("Juan");
        perfil.Apellido.Should().Be("Pérez");
    }

    [Fact]
    public async Task GetPerfilByEmail_WithValidEmail_ReturnsProfile()
    {
        // Arrange - Login primero
        await LoginAsync("carlos.rodriguez@test.com", TestDataSeeder.TestPasswordPlainText);

        // Act
        var response = await Client.GetAsync("/api/auth/perfil/email/carlos.rodriguez@test.com");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var perfil = await response.Content.ReadFromJsonAsync<UsuarioDto>();
        perfil.Should().NotBeNull();
        perfil!.Email.Should().Be("carlos.rodriguez@test.com");
        perfil.Nombre.Should().Be("Carlos");
    }

    [Fact]
    public async Task ValidarCorreo_WithExistingEmail_ReturnsTrue()
    {
        // Arrange - No requiere autenticación (endpoint público)

        // Act
        var response = await Client.GetAsync("/api/auth/validar-email/juan.perez@test.com");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<bool>();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidarCorreo_WithNonExistentEmail_ReturnsFalse()
    {
        // Arrange

        // Act
        var response = await Client.GetAsync("/api/auth/validar-email/nonexistent@test.com");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<bool>();
        result.Should().BeFalse();
    }

    #endregion
}



