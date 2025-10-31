using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ActivateAccount;
using MiGenteEnLinea.Application.Features.Authentication.Commands.AddProfileInfo;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ChangePasswordById;
using MiGenteEnLinea.Application.Features.Authentication.Commands.DeleteUser;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ForgotPassword;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Login;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Register;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ResendActivationEmail;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ResetPassword;
using MiGenteEnLinea.Application.Features.Authentication.Commands.UpdateCredencial;
using MiGenteEnLinea.Application.Features.Authentication.Commands.UpdateProfile;
using MiGenteEnLinea.Application.Features.Authentication.Commands.UpdateProfileExtended;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using MiGenteEnLinea.Domain.ValueObjects;
using MiGenteEnLinea.Infrastructure.Persistence.Contexts;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for ALL Authentication Commands
/// Tests complete flows with real database operations
/// </summary>
[Collection("Integration Tests")]
public class AuthenticationCommandsTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public AuthenticationCommandsTests(
        TestWebApplicationFactory factory,
        ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    #region Helper Methods

    private async Task<(string userId, string email)> CreateTestUserAsync(
        string email,
        string password = "Test123!",
        bool isActive = false)
    {
        var command = new RegisterCommand
        {
            Email = email,
            Password = password,
            Nombre = "Test",
            Apellido = "User",
            Tipo = 1, // Empleador
            Host = "http://localhost:5015"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", command);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RegisterResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();

        // Get UserId (GUID) from Credencial using CredentialId
        var credencial = await AppDbContext.Credenciales
            .FirstOrDefaultAsync(c => c.Id == result.CredentialId!.Value);
        
        if (credencial == null)
            throw new InvalidOperationException($"Credencial not found for ID {result.CredentialId}");

        if (isActive)
        {
            // Activate the account with correct UserId (GUID)
            var activateCommand = new ActivateAccountCommand
            {
                UserId = credencial.UserId, // ✅ GUID string, not CredentialId
                Email = email
            };
            var activateResponse = await Client.PostAsJsonAsync("/api/auth/activate", activateCommand);
            activateResponse.EnsureSuccessStatusCode();
        }

        // Return UserId (GUID) for commands that need it
        return (credencial.UserId, email);
    }

    private async Task<string> GetAuthTokenAsync(string email, string password)
    {
        var loginCommand = new LoginCommand
        {
            Email = email,
            Password = password,
            IpAddress = "127.0.0.1"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();

        return result.AccessToken;
    }

    #endregion

    #region ActivateAccountCommand Tests

    [Fact]
    public async Task ActivateAccount_WithValidUserIdAndEmail_ShouldActivateSuccessfully()
    {
        // Arrange
        var email = $"activate-test-{Guid.NewGuid()}@test.com";
        var (userId, _) = await CreateTestUserAsync(email, isActive: false);

        var command = new ActivateAccountCommand
        {
            UserId = userId,
            Email = email
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/activate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("activada exitosamente");

        // Verify in database - Use AsNoTracking to bypass EF cache and get fresh data
        var emailVO = Email.CreateUnsafe(email);
        var credencial = await AppDbContext.Credenciales
            .AsNoTracking() // ✅ Bypass cache to get actual DB state
            .FirstOrDefaultAsync(c => c.Email == emailVO);

        credencial.Should().NotBeNull();
        credencial!.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateAccount_WithInvalidUserId_ShouldReturnBadRequest()
    {
        // Arrange - Uso un userId que NO existe en la base de datos
        var command = new ActivateAccountCommand
        {
            UserId = Guid.NewGuid().ToString(),
            Email = "nonexistent@test.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/activate", command);

        // Assert - La API retorna BadRequest cuando no encuentra el usuario
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("No se pudo activar"); // Uppercase "No"
    }

    [Fact]
    public async Task ActivateAccount_WithAlreadyActiveUser_ShouldReturnOK()
    {
        // Arrange
        var email = $"already-active-{Guid.NewGuid()}@test.com";
        var (userId, _) = await CreateTestUserAsync(email, isActive: true);

        var command = new ActivateAccountCommand
        {
            UserId = userId,
            Email = email
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/activate", command);

        // Assert - Handler catches domain exception and returns true (user already active in Identity)
        response.StatusCode.Should().Be(HttpStatusCode.OK); // Handler logs error but returns true
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("activada exitosamente");
    }

    [Fact]
    public async Task ActivateAccount_WithMismatchedEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var email = $"mismatch-{Guid.NewGuid()}@test.com";
        var (userId, _) = await CreateTestUserAsync(email, isActive: false);

        var command = new ActivateAccountCommand
        {
            UserId = userId,
            Email = "wrong@test.com" // Email diferente al registrado
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/activate", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("No se pudo activar"); // Uppercase "No"
    }

    #endregion

    #region ForgotPassword + ResetPassword Flow Tests

    [Fact]
    public async Task ForgotPassword_ResetPassword_CompleteFlow_ShouldSucceed()
    {
        // Arrange
        var email = $"forgot-pwd-{Guid.NewGuid()}@test.com";
        var originalPassword = "Original123!";
        var newPassword = "NewPassword456!";
        var (userId, _) = await CreateTestUserAsync(email, originalPassword, isActive: true);

        // Act 1: Request password reset
        var forgotCommand = new ForgotPasswordCommand { Email = email };
        var forgotResponse = await Client.PostAsJsonAsync("/api/auth/forgot-password", forgotCommand);

        // Assert 1
        forgotResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Get the reset token from database
        var resetToken = await AppDbContext.PasswordResetTokens
            .Where(t => t.Email == email && t.UsedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.Token)
            .FirstOrDefaultAsync();

        resetToken.Should().NotBeNull();

        // Act 2: Reset password with token
        var resetCommand = new ResetPasswordCommand
        {
            Email = email,
            Token = resetToken!,
            NewPassword = newPassword
        };
        var resetResponse = await Client.PostAsJsonAsync("/api/auth/reset-password", resetCommand);

        // Assert 2
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 3: Try login with new password
        var token = await GetAuthTokenAsync(email, newPassword);

        // Assert 3
        token.Should().NotBeNullOrEmpty();

        // Act 4: Verify old password no longer works
        var oldPasswordResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginCommand
        {
            Email = email,
            Password = originalPassword
        });

        // Assert 4
        oldPasswordResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentEmail_ShouldReturnNotFound()
    {
        // Arrange
        var command = new ForgotPasswordCommand
        {
            Email = "nonexistent@test.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ShouldReturnBadRequest()
    {
        // Arrange
        var email = $"invalid-token-{Guid.NewGuid()}@test.com";
        await CreateTestUserAsync(email, isActive: true);

        var command = new ResetPasswordCommand
        {
            Email = email,
            Token = "999999", // Invalid token
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(Skip = "PasswordResetToken.ExpiresAt is readonly - cannot manually expire. Would need to wait 15 minutes or mock time.")]
    public async Task ResetPassword_WithExpiredToken_ShouldReturnBadRequest()
    {
        // Arrange
        var email = $"expired-token-{Guid.NewGuid()}@test.com";
        await CreateTestUserAsync(email, isActive: true);

        // Request password reset
        var forgotCommand = new ForgotPasswordCommand { Email = email };
        await Client.PostAsJsonAsync("/api/auth/forgot-password", forgotCommand);

        // Get token and manually expire it in database
        var tokenEntity = await AppDbContext.PasswordResetTokens
            .Where(t => t.Email == email && t.UsedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        tokenEntity.Should().NotBeNull();

        // TODO: Cannot modify readonly property tokenEntity.ExpiresAt
        // Would need to:
        // Option 1: Use IDateTime mock and wait 15 minutes
        // Option 2: Create token directly with expired time
        // Option 3: Mock system clock in test

        var command = new ResetPasswordCommand
        {
            Email = email,
            Token = tokenEntity!.Token,
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region ResendActivationEmail Tests

    [Fact]
    public async Task ResendActivationEmail_ForInactiveUser_ShouldSucceed()
    {
        // Arrange
        var email = $"resend-{Guid.NewGuid()}@test.com";
        var (userId, _) = await CreateTestUserAsync(email, isActive: false);

        var command = new ResendActivationEmailCommand
        {
            UserId = userId,
            Email = email,
            Host = "https://localhost:5001"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/resend-activation", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("Email de activación");
    }

    [Fact]
    public async Task ResendActivationEmail_ForAlreadyActiveUser_ShouldReturnBadRequest()
    {
        // Arrange
        var email = $"resend-active-{Guid.NewGuid()}@test.com";
        var (userId, _) = await CreateTestUserAsync(email, isActive: true);

        var command = new ResendActivationEmailCommand
        {
            UserId = userId,
            Email = email,
            Host = "https://localhost:5001"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/resend-activation", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region UpdateCredencial Tests

    [Fact]
    public async Task UpdateCredencial_ChangeEmailAndPassword_ShouldSucceed()
    {
        // Arrange
        var originalEmail = $"original-{Guid.NewGuid()}@test.com";
        var newEmail = $"updated-{Guid.NewGuid()}@test.com";
        var originalPassword = "Original123!";
        var newPassword = "Updated456!";
        var (userId, _) = await CreateTestUserAsync(originalEmail, originalPassword, isActive: true);

        var command = new UpdateCredencialCommand
        {
            UserId = userId,
            Email = newEmail,
            Password = newPassword,
            Activo = true
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/auth/credenciales", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("actualizada");

        // Verify can login with new credentials
        var token = await GetAuthTokenAsync(newEmail, newPassword);
        token.Should().NotBeNullOrEmpty();

        // Verify old email doesn't work
        var oldEmailResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginCommand
        {
            Email = originalEmail,
            Password = newPassword
        });
        oldEmailResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateCredencial_DeactivateUser_ShouldPreventLogin()
    {
        // Arrange
        var email = $"deactivate-{Guid.NewGuid()}@test.com";
        var password = "Test123!";
        var (userId, _) = await CreateTestUserAsync(email, password, isActive: true);

        var command = new UpdateCredencialCommand
        {
            UserId = userId,
            Email = email,
            Password = null, // Don't change password
            Activo = false // Deactivate
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/auth/credenciales", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("actualizada");

        // Verify cannot login
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginCommand
        {
            Email = email,
            Password = password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteUser Tests

    [Fact]
    public async Task DeleteUser_SoftDelete_ShouldPreventLogin()
    {
        // Arrange
        var email = $"delete-{Guid.NewGuid()}@test.com";
        var password = "Test123!";
        var (userId, _) = await CreateTestUserAsync(email, password, isActive: true);

        // Get credencial ID
        var emailVO = Email.CreateUnsafe(email);
        var credencial = await AppDbContext.Credenciales
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == emailVO);
        credencial.Should().NotBeNull();

        var command = new DeleteUserCommand
        {
            UserID = userId,
            CredencialID = credencial!.Id
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/delete-user", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("eliminado");

        // CRITICAL: Clear EF Core ChangeTracker to force fresh database query
        // Without this, EF returns cached entity with old Activo=true value
        ClearChangeTracker();

        // Verify user is marked inactive in Legacy Credenciales
        // Use separate DbContext to avoid any caching issues
        using var scope = Factory.Services.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<MiGenteDbContext>();
        
        var deletedCredencial = await freshContext.CredencialesRefactored
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == emailVO);
        
        deletedCredencial.Should().NotBeNull();
        deletedCredencial!.Activo.Should().BeFalse();

        // Verify cannot login
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginCommand
        {
            Email = email,
            Password = password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region AddProfileInfo Tests

    [Fact]
    public async Task AddProfileInfo_WithValidData_ShouldCreateProfileInfo()
    {
        // Arrange
        var email = $"profile-info-{Guid.NewGuid()}@test.com";
        var (userId, _) = await CreateTestUserAsync(email, isActive: true);

        var command = new AddProfileInfoCommand(
            UserId: userId,
            Identificacion: "00112233445",
            TipoIdentificacion: 1, // Cédula
            NombreComercial: "Test Company",
            Direccion: "123 Test Street",
            Presentacion: "Test presentation"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/profile-info", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        result.Should().NotBeNull();
        result.Should().ContainKey("id");
        
        // JsonElement requires GetInt32() instead of Convert.ToInt32()
        var idElement = (System.Text.Json.JsonElement)result!["id"];
        var profileInfoId = idElement.GetInt32();
        profileInfoId.Should().BeGreaterThan(0);

        // Verify in database
        var profileInfo = await AppDbContext.PerfilesInfos
            .FirstOrDefaultAsync(p => p.Id == profileInfoId);

        profileInfo.Should().NotBeNull();
        profileInfo!.Identificacion.Should().Be("00112233445");
        profileInfo.NombreComercial.Should().Be("Test Company");
    }

    #endregion

    #region UpdateProfile Tests

    [Fact]
    public async Task UpdateProfile_WithValidData_ShouldUpdateSuccessfully()
    {
        // Arrange
        var email = $"update-profile-{Guid.NewGuid()}@test.com";
        var (userId, _) = await CreateTestUserAsync(email, isActive: true);

        var command = new UpdateProfileCommand
        {
            UserID = userId,
            Nombre = "Updated",
            Apellido = "Name",
            Email = email
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/auth/perfil/{userId}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("actualizado");

        // Verify in database
        var perfil = await AppDbContext.Perfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        perfil.Should().NotBeNull();
        perfil!.Nombre.Should().Be("Updated");
        perfil.Apellido.Should().Be("Name");
    }

    #endregion

    #region UpdateProfileExtended Tests

    [Fact]
    public async Task UpdateProfileExtended_WithFullData_ShouldUpdateBothTables()
    {
        // Arrange
        var email = $"extended-{Guid.NewGuid()}@test.com";
        var (userId, _) = await CreateTestUserAsync(email, isActive: true);

        var command = new UpdateProfileExtendedCommand
        {
            UserId = userId,
            Nombre = "Extended",
            Apellido = "Test",
            Email = email,
            Telefono1 = "8091234567",
            Identificacion = "00112233445",
            TipoIdentificacion = 1,
            Direccion = "Updated Address"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/auth/perfil-completo/{userId}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("actualizado");

        // Verify Perfile updated
        var perfil = await AppDbContext.Perfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        perfil.Should().NotBeNull();
        perfil!.Nombre.Should().Be("Extended");
        perfil.Telefono1.Should().Be("8091234567");

        // Verify PerfilesInfo updated
        var profileInfo = await AppDbContext.PerfilesInfos
            .FirstOrDefaultAsync(p => p.UserId == userId);

        profileInfo.Should().NotBeNull();
        profileInfo!.Identificacion.Should().Be("00112233445");
        profileInfo.Direccion.Should().Be("Updated Address");
    }

    #endregion

    #region ChangePasswordById Tests

    [Fact]
    public async Task ChangePasswordById_WithValidCredencialId_ShouldChangePassword()
    {
        // Arrange
        var email = $"change-pwd-id-{Guid.NewGuid()}@test.com";
        var oldPassword = "Old123!";
        var newPassword = "New456!";
        var (userId, _) = await CreateTestUserAsync(email, oldPassword, isActive: true);

        // Get credencial ID
        var emailVO = Email.CreateUnsafe(email);
        var credencial = await AppDbContext.Credenciales
            .FirstOrDefaultAsync(c => c.Email == emailVO);
        credencial.Should().NotBeNull();

        var command = new ChangePasswordByIdCommand
        {
            CredencialId = credencial!.Id,
            NewPassword = newPassword
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password-by-id", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["message"].Should().Contain("cambiada");

        // Verify can login with new password
        var token = await GetAuthTokenAsync(email, newPassword);
        token.Should().NotBeNullOrEmpty();

        // Verify old password doesn't work
        var oldPasswordResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginCommand
        {
            Email = email,
            Password = oldPassword
        });
        oldPasswordResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePasswordById_WithInvalidCredencialId_ShouldReturnNotFound()
    {
        // Arrange
        var command = new ChangePasswordByIdCommand
        {
            CredencialId = 999999,
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/change-password-by-id", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
