using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiGenteEnLinea.Application.Features.Authentication.Commands.ChangePassword;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Login;
using MiGenteEnLinea.Application.Features.Authentication.Commands.RefreshToken;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Register;
using MiGenteEnLinea.Application.Features.Authentication.Commands.RevokeToken;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using MiGenteEnLinea.Domain.ValueObjects;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Tests de flujos completos de autenticación siguiendo el orden lógico del usuario:
/// 1. Register → 2. Login → 3. ActivateAccount → 4. ChangePassword → etc.
/// 
/// OBJETIVO: Validar el patrón Identity-First + Legacy Sync en todo el flujo.
/// </summary>
[Collection("Integration Tests")]
public class AuthFlowTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public AuthFlowTests(TestWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    /// <summary>
    /// Flujo 1: Register → Login
    /// Valida que un usuario recién registrado pueda hacer login inmediatamente.
    /// </summary>
    [Fact]
    public async Task Flow_RegisterAndLogin_Success()
    {
        // ====================================================================
        // PASO 1: REGISTER (crea usuario en Identity + Legacy)
        // ====================================================================
        var email = GenerateUniqueEmail("flow-test");
        var password = "FlowTest@123";
        
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = password,
            Nombre = "Test",
            Apellido = "Flow",
            Tipo = 1, // Empleador
            Host = "http://localhost:5015"
        };

        _output.WriteLine($"[PASO 1] Registrando usuario: {email}");
        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);
        
        registerResponse.IsSuccessStatusCode.Should().BeTrue();
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResult>();
        
        registerResult.Should().NotBeNull();
        registerResult!.Success.Should().BeTrue();
        registerResult.UserId.Should().NotBeNullOrEmpty();
        
        var userId = registerResult.UserId!;
        _output.WriteLine($"[PASO 1] ✅ Usuario registrado. UserId: {userId}");

        // ====================================================================
        // VERIFICAR: Usuario existe en Identity (AspNetUsers)
        // ====================================================================
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MiGenteEnLinea.Infrastructure.Identity.ApplicationUser>>();
        
        var userInIdentity = await userManager.FindByEmailAsync(email);
        userInIdentity.Should().NotBeNull("El usuario debe existir en AspNetUsers (Identity)");
        userInIdentity!.Id.Should().Be(userId);
        _output.WriteLine($"[VERIFY] ✅ Usuario encontrado en AspNetUsers (Identity)");

        // ====================================================================
        // VERIFICAR: Usuario existe en Legacy (Credenciales + Perfiles)
        // ====================================================================
        var credencial = await AppDbContext.Credenciales
            .FirstOrDefaultAsync(c => c.UserId == userId);
        credencial.Should().NotBeNull("El usuario debe existir en Credenciales (Legacy)");
        
        var perfil = await AppDbContext.Perfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
        perfil.Should().NotBeNull("El usuario debe existir en Perfiles (Legacy)");
        _output.WriteLine($"[VERIFY] ✅ Usuario encontrado en Credenciales + Perfiles (Legacy)");

        // ====================================================================
        // PASO 1.5: ACTIVAR CUENTA (simular click en email de activación)
        // ====================================================================
        _output.WriteLine($"[PASO 1.5] Activando cuenta para: {email}");
        
        // Activar usando UserManager directamente (simula ActivateAccountCommand)
        userInIdentity!.EmailConfirmed = true;
        var updateResult = await userManager.UpdateAsync(userInIdentity);
        updateResult.Succeeded.Should().BeTrue("La activación debe ser exitosa");
        
        // También activar en Legacy (sincronización)
        credencial!.Activar();
        await AppDbContext.SaveChangesAsync();
        
        _output.WriteLine($"[PASO 1.5] ✅ Cuenta activada en Identity + Legacy");

        // ====================================================================
        // PASO 2: LOGIN (autentica con Identity-First)
        // ====================================================================
        _output.WriteLine($"[PASO 2] Intentando login con: {email}");
        
        var loginCommand = new LoginCommand
        {
            Email = email,
            Password = password,
            IpAddress = "127.0.0.1"
        };

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);
        
        loginResponse.IsSuccessStatusCode.Should().BeTrue();
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        
        loginResult.Should().NotBeNull();
        loginResult!.AccessToken.Should().NotBeNullOrEmpty();
        loginResult.RefreshToken.Should().NotBeNullOrEmpty();
        loginResult.User.Should().NotBeNull();
        loginResult.User!.UserId.Should().Be(userId);
        loginResult.User.Email.Should().Be(email);
        
        _output.WriteLine($"[PASO 2] ✅ Login exitoso. AccessToken: {loginResult.AccessToken[..20]}...");

        // ====================================================================
        // RESULTADO FINAL
        // ====================================================================
        _output.WriteLine("[RESULTADO] ✅ Flujo Register → Login completado exitosamente");
    }

    /// <summary>
    /// Flujo 2: Login con usuario Legacy (migracion automatica a Identity)
    /// Valida el patrón Legacy Fallback: usuario existe solo en Credenciales (Legacy),
    /// se migra automáticamente a Identity al hacer login.
    /// </summary>
    [Fact]
    public async Task Flow_LoginLegacyUser_AutoMigratesToIdentity()
    {
        // ====================================================================
        // SETUP: Usuario "juan.perez@test.com" existe SOLO en Legacy
        // (creado por TestDataSeeder en Credenciales + Perfiles)
        // ====================================================================
        var email = "juan.perez@test.com";
        var password = TestDataSeeder.TestPasswordPlainText; // "Test@1234"

        _output.WriteLine($"[SETUP] Usuario legacy: {email}");

        // Verificar que NO existe en Identity (antes de login)
        using var scopeBefore = Factory.Services.CreateScope();
        var userManagerBefore = scopeBefore.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MiGenteEnLinea.Infrastructure.Identity.ApplicationUser>>();
        var userBeforeLogin = await userManagerBefore.FindByEmailAsync(email);
        _output.WriteLine($"[SETUP] Usuario en Identity (antes de login): {userBeforeLogin?.Email ?? "NULL"}");

        // Verificar que SÍ existe en Legacy
        // ✅ OPTIMIZADO: Usar Value Object comparison (EF Core puede traducir esto)
        // Patrón recomendado para producción: comparar Value Objects directamente
        var emailVO = Email.CreateUnsafe(email);
        var credencial = await AppDbContext.Credenciales
            .FirstOrDefaultAsync(c => c.Email == emailVO);
        credencial.Should().NotBeNull("El usuario debe existir en Credenciales (Legacy)");
        _output.WriteLine($"[SETUP] ✅ Usuario encontrado en Credenciales (Legacy). UserId: {credencial!.UserId}");

        // ====================================================================
        // PASO 1: LOGIN (debería buscar en Legacy y migrar a Identity)
        // ====================================================================
        _output.WriteLine($"[PASO 1] Intentando login con usuario Legacy: {email}");

        var loginCommand = new LoginCommand
        {
            Email = email,
            Password = password,
            IpAddress = "127.0.0.1"
        };

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        loginResponse.IsSuccessStatusCode.Should().BeTrue();
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();

        loginResult.Should().NotBeNull();
        loginResult!.AccessToken.Should().NotBeNullOrEmpty();
        loginResult.RefreshToken.Should().NotBeNullOrEmpty();

        _output.WriteLine($"[PASO 1] ✅ Login exitoso. AccessToken: {loginResult.AccessToken[..20]}...");

        // ====================================================================
        // VERIFICAR: Usuario ahora existe en Identity (migrado automáticamente)
        // ====================================================================
        using var scopeAfter = Factory.Services.CreateScope();
        var userManagerAfter = scopeAfter.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MiGenteEnLinea.Infrastructure.Identity.ApplicationUser>>();
        var userAfterLogin = await userManagerAfter.FindByEmailAsync(email);
        userAfterLogin.Should().NotBeNull("El usuario debería haberse migrado a AspNetUsers (Identity)");
        userAfterLogin!.Email.Should().Be(email);
        userAfterLogin.Id.Should().Be(credencial.UserId, "El UserId debe ser el mismo que en Legacy");

        _output.WriteLine($"[VERIFY] ✅ Usuario migrado a Identity. UserId: {userAfterLogin.Id}");

        // ====================================================================
        // RESULTADO FINAL
        // ====================================================================
        _output.WriteLine("[RESULTADO] ✅ Flujo Legacy Fallback → Auto-Migration completado exitosamente");
    }

    /// <summary>
    /// Flujo 3: Login con credenciales inválidas (password incorrecto)
    /// </summary>
    [Fact]
    public async Task Flow_Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var email = "juan.perez@test.com";
        var wrongPassword = "WrongPassword@123";

        var loginCommand = new LoginCommand
        {
            Email = email,
            Password = wrongPassword,
            IpAddress = "127.0.0.1"
        };

        _output.WriteLine($"[TEST] Intentando login con password incorrecto: {email}");

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _output.WriteLine("[RESULTADO] ✅ Login rechazado correctamente (Unauthorized)");
    }

    /// <summary>
    /// Flujo 4: Login con email no existente
    /// </summary>
    [Fact]
    public async Task Flow_Login_WithNonExistentEmail_ReturnsUnauthorized()
    {
        var nonExistentEmail = "nonexistent@test.com";
        var password = "SomePassword@123";

        var loginCommand = new LoginCommand
        {
            Email = nonExistentEmail,
            Password = password,
            IpAddress = "127.0.0.1"
        };

        _output.WriteLine($"[TEST] Intentando login con email inexistente: {nonExistentEmail}");

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _output.WriteLine("[RESULTADO] ✅ Login rechazado correctamente (Unauthorized)");
    }

    /// <summary>
    /// Flujo 5: Register → Activate → ChangePassword → Login
    /// Valida el flujo completo de cambio de contraseña después de activación.
    /// </summary>
    [Fact]
    public async Task Flow_Register_Activate_ChangePassword_Login_Success()
    {
        // ====================================================================
        // PASO 1: REGISTER
        // ====================================================================
        var email = GenerateUniqueEmail("change-pwd-test");
        var originalPassword = "Original@123";
        var newPassword = "NewPassword@456";
        
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = originalPassword,
            Nombre = "Change",
            Apellido = "Password",
            Tipo = 1, // Empleador
            Host = "http://localhost:5015"
        };

        _output.WriteLine($"[PASO 1] Registrando usuario: {email}");
        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);
        registerResponse.EnsureSuccessStatusCode();
        
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResult>();
        var userId = registerResult!.UserId!;
        _output.WriteLine($"[PASO 1] ✅ Usuario registrado. UserId: {userId}");

        // ====================================================================
        // PASO 2: ACTIVAR CUENTA
        // ====================================================================
        _output.WriteLine($"[PASO 2] Activando cuenta...");
        
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MiGenteEnLinea.Infrastructure.Identity.ApplicationUser>>();
        
        var user = await userManager.FindByIdAsync(userId);
        user!.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        
        // Sincronizar con Legacy
        var emailVO = Email.CreateUnsafe(email);
        var credencial = await AppDbContext.Credenciales.FirstOrDefaultAsync(c => c.Email == emailVO);
        credencial!.Activar();
        await AppDbContext.SaveChangesAsync();
        
        _output.WriteLine($"[PASO 2] ✅ Cuenta activada");

        // ====================================================================
        // PASO 3: LOGIN CON PASSWORD ORIGINAL
        // ====================================================================
        _output.WriteLine($"[PASO 3] Login con password original...");
        
        var loginCommand1 = new LoginCommand
        {
            Email = email,
            Password = originalPassword,
            IpAddress = "127.0.0.1"
        };

        var loginResponse1 = await Client.PostAsJsonAsync("/api/auth/login", loginCommand1);
        loginResponse1.EnsureSuccessStatusCode();
        
        var loginResult1 = await loginResponse1.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        var accessToken = loginResult1!.AccessToken;
        
        _output.WriteLine($"[PASO 3] ✅ Login exitoso con password original");

        // ====================================================================
        // PASO 4: CHANGE PASSWORD
        // ====================================================================
        _output.WriteLine($"[PASO 4] Cambiando contraseña...");
        
        var changePasswordCommand = new ChangePasswordCommand(
            Email: email,
            UserId: userId,
            CurrentPassword: originalPassword,
            NewPassword: newPassword
        );

        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var changePasswordResponse = await Client.PostAsJsonAsync("/api/auth/change-password", changePasswordCommand);
        changePasswordResponse.EnsureSuccessStatusCode();
        
        _output.WriteLine($"[PASO 4] ✅ Contraseña cambiada exitosamente");

        // ====================================================================
        // PASO 5: LOGIN CON NUEVA PASSWORD
        // ====================================================================
        _output.WriteLine($"[PASO 5] Login con nueva contraseña...");
        
        Client.DefaultRequestHeaders.Authorization = null; // Remove token
        
        var loginCommand2 = new LoginCommand
        {
            Email = email,
            Password = newPassword,
            IpAddress = "127.0.0.1"
        };

        var loginResponse2 = await Client.PostAsJsonAsync("/api/auth/login", loginCommand2);
        loginResponse2.EnsureSuccessStatusCode();
        
        var loginResult2 = await loginResponse2.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        loginResult2.Should().NotBeNull();
        loginResult2!.AccessToken.Should().NotBeNullOrEmpty();
        
        _output.WriteLine($"[PASO 5] ✅ Login exitoso con nueva contraseña");

        // ====================================================================
        // PASO 6: VERIFICAR QUE PASSWORD ANTIGUA YA NO FUNCIONA
        // ====================================================================
        _output.WriteLine($"[PASO 6] Verificando que password antigua no funciona...");
        
        var loginCommand3 = new LoginCommand
        {
            Email = email,
            Password = originalPassword, // Old password
            IpAddress = "127.0.0.1"
        };

        var loginResponse3 = await Client.PostAsJsonAsync("/api/auth/login", loginCommand3);
        loginResponse3.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        _output.WriteLine($"[PASO 6] ✅ Password antigua correctamente rechazada");

        // ====================================================================
        // RESULTADO FINAL
        // ====================================================================
        _output.WriteLine("[RESULTADO] ✅ Flujo Register → Activate → ChangePassword → Login completado exitosamente");
    }

    /// <summary>
    /// Flujo 6: Login → RefreshToken → Verify new access token
    /// Valida el mecanismo de refresh tokens para renovar access tokens expirados.
    /// </summary>
    [Fact]
    public async Task Flow_Login_RefreshToken_Success()
    {
        // ====================================================================
        // SETUP: Usuario activado y listo para login
        // ====================================================================
        var email = GenerateUniqueEmail("refresh-token-test");
        var password = "RefreshTest@123";
        
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = password,
            Nombre = "Refresh",
            Apellido = "Token",
            Tipo = 2, // Contratista
            Host = "http://localhost:5015"
        };

        _output.WriteLine($"[SETUP] Registrando y activando usuario: {email}");
        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);
        registerResponse.EnsureSuccessStatusCode();
        
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResult>();
        var userId = registerResult!.UserId!;

        // Activar cuenta
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MiGenteEnLinea.Infrastructure.Identity.ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        user!.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        
        var emailVO = Email.CreateUnsafe(email);
        var credencial = await AppDbContext.Credenciales.FirstOrDefaultAsync(c => c.Email == emailVO);
        credencial!.Activar();
        await AppDbContext.SaveChangesAsync();
        
        _output.WriteLine($"[SETUP] ✅ Usuario activado");

        // ====================================================================
        // PASO 1: LOGIN (obtener access token + refresh token)
        // ====================================================================
        _output.WriteLine($"[PASO 1] Login inicial...");
        
        var loginCommand = new LoginCommand
        {
            Email = email,
            Password = password,
            IpAddress = "127.0.0.1"
        };

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);
        loginResponse.EnsureSuccessStatusCode();
        
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        loginResult.Should().NotBeNull();
        
        var firstAccessToken = loginResult!.AccessToken;
        var firstRefreshToken = loginResult.RefreshToken;
        
        firstAccessToken.Should().NotBeNullOrEmpty();
        firstRefreshToken.Should().NotBeNullOrEmpty();
        
        _output.WriteLine($"[PASO 1] ✅ Login exitoso");
        _output.WriteLine($"[PASO 1] AccessToken (primeros 30 chars): {firstAccessToken[..Math.Min(30, firstAccessToken.Length)]}...");
        _output.WriteLine($"[PASO 1] RefreshToken (primeros 30 chars): {firstRefreshToken[..Math.Min(30, firstRefreshToken.Length)]}...");

        // ====================================================================
        // PASO 2: REFRESH TOKEN (obtener nuevo access token)
        // ====================================================================
        _output.WriteLine($"[PASO 2] Refrescando access token...");
        
        var refreshTokenCommand = new RefreshTokenCommand(
            RefreshToken: firstRefreshToken,
            IpAddress: "127.0.0.1"
        );

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", refreshTokenCommand);
        refreshResponse.EnsureSuccessStatusCode();
        
        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        refreshResult.Should().NotBeNull();
        
        var newAccessToken = refreshResult!.AccessToken;
        var newRefreshToken = refreshResult.RefreshToken;
        
        newAccessToken.Should().NotBeNullOrEmpty();
        newRefreshToken.Should().NotBeNullOrEmpty();
        newAccessToken.Should().NotBe(firstAccessToken, "El nuevo access token debe ser diferente");
        
        _output.WriteLine($"[PASO 2] ✅ Token refrescado exitosamente");
        _output.WriteLine($"[PASO 2] Nuevo AccessToken (primeros 30 chars): {newAccessToken[..Math.Min(30, newAccessToken.Length)]}...");

        // ====================================================================
        // PASO 3: VERIFICAR NUEVO ACCESS TOKEN (hacer request autenticado)
        // ====================================================================
        _output.WriteLine($"[PASO 3] Verificando nuevo access token...");
        
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newAccessToken);

        var profileResponse = await Client.GetAsync($"/api/auth/perfil/{userId}");
        profileResponse.EnsureSuccessStatusCode();
        
        var profile = await profileResponse.Content.ReadFromJsonAsync<PerfilDto>();
        profile.Should().NotBeNull();
        profile!.UserId.Should().Be(userId);
        profile.Email.Should().Be(email);
        
        _output.WriteLine($"[PASO 3] ✅ Nuevo access token funcionando correctamente");
        _output.WriteLine($"[PASO 3] Perfil obtenido: {profile.Nombre} {profile.Apellido}");

        // ====================================================================
        // RESULTADO FINAL
        // ====================================================================
        _output.WriteLine("[RESULTADO] ✅ Flujo Login → RefreshToken completado exitosamente");
    }

    /// <summary>
    /// Flujo 7: Login → Logout (RevokeToken) → Verify token is invalid
    /// Valida el mecanismo de revocación de tokens para logout seguro.
    /// </summary>
    [Fact]
    public async Task Flow_Login_Logout_RevokeToken_Success()
    {
        // ====================================================================
        // SETUP: Usuario activado
        // ====================================================================
        var email = GenerateUniqueEmail("revoke-token-test");
        var password = "RevokeTest@123";
        
        var registerCommand = new RegisterCommand
        {
            Email = email,
            Password = password,
            Nombre = "Revoke",
            Apellido = "Token",
            Tipo = 1, // Empleador
            Host = "http://localhost:5015"
        };

        _output.WriteLine($"[SETUP] Registrando y activando usuario: {email}");
        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register", registerCommand);
        registerResponse.EnsureSuccessStatusCode();
        
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResult>();
        var userId = registerResult!.UserId!;

        // Activar cuenta
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MiGenteEnLinea.Infrastructure.Identity.ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        user!.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        
        var emailVO = Email.CreateUnsafe(email);
        var credencial = await AppDbContext.Credenciales.FirstOrDefaultAsync(c => c.Email == emailVO);
        credencial!.Activar();
        await AppDbContext.SaveChangesAsync();
        
        _output.WriteLine($"[SETUP] ✅ Usuario activado");

        // ====================================================================
        // PASO 1: LOGIN
        // ====================================================================
        _output.WriteLine($"[PASO 1] Login...");
        
        var loginCommand = new LoginCommand
        {
            Email = email,
            Password = password,
            IpAddress = "127.0.0.1"
        };

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginCommand);
        loginResponse.EnsureSuccessStatusCode();
        
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        var accessToken = loginResult!.AccessToken;
        var refreshToken = loginResult.RefreshToken;
        
        _output.WriteLine($"[PASO 1] ✅ Login exitoso");

        // ====================================================================
        // PASO 2: VERIFICAR TOKEN FUNCIONA (antes de revoke)
        // ====================================================================
        _output.WriteLine($"[PASO 2] Verificando que token funciona antes de revocar...");
        
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var profileResponse1 = await Client.GetAsync($"/api/auth/perfil/{userId}");
        profileResponse1.EnsureSuccessStatusCode();
        
        _output.WriteLine($"[PASO 2] ✅ Token funcionando correctamente (antes de revoke)");

        // ====================================================================
        // PASO 3: REVOKE TOKEN (logout)
        // ====================================================================
        _output.WriteLine($"[PASO 3] Revocando token (logout)...");
        
        var revokeCommand = new RevokeTokenCommand(
            RefreshToken: refreshToken,
            IpAddress: "127.0.0.1",
            Reason: "User logout test"
        );

        var revokeResponse = await Client.PostAsJsonAsync("/api/auth/revoke", revokeCommand);
        revokeResponse.EnsureSuccessStatusCode();
        
        _output.WriteLine($"[PASO 3] ✅ Token revocado exitosamente");

        // ====================================================================
        // PASO 4: VERIFICAR QUE REFRESH TOKEN YA NO FUNCIONA
        // ====================================================================
        _output.WriteLine($"[PASO 4] Intentando usar refresh token revocado...");
        
        var refreshCommand = new RefreshTokenCommand(
            RefreshToken: refreshToken,
            IpAddress: "127.0.0.1"
        );

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", refreshCommand);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized, 
            "El refresh token revocado debe rechazarse");
        
        _output.WriteLine($"[PASO 4] ✅ Refresh token revocado correctamente rechazado");

        // ====================================================================
        // RESULTADO FINAL
        // ====================================================================
        _output.WriteLine("[RESULTADO] ✅ Flujo Login → Logout (RevokeToken) completado exitosamente");
    }
}
