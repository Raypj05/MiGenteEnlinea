using System.Net.Http.Headers;
using System.Net.Http.Json;
using MiGenteEnLinea.Application.Features.Authentication.Commands.Login;
using MiGenteEnLinea.Application.Features.Authentication.DTOs;
using FluentAssertions;

namespace MiGenteEnLinea.IntegrationTests.Infrastructure;

/// <summary>
/// Helper para operaciones comunes en tests de integración:
/// - Autenticación de usuarios
/// - Configuración de headers JWT
/// - Helpers para aserciones comunes
/// </summary>
public static class IntegrationTestHelper
{
    /// <summary>
    /// Autentica un usuario y retorna el token JWT
    /// </summary>
    public static async Task<string> AuthenticateAsync(
        HttpClient client, 
        string email, 
        string password = TestDataSeeder.TestPasswordPlainText)
    {
        var loginCommand = new LoginCommand
        {
            Email = email,
            Password = password,
            IpAddress = "127.0.0.1"
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", loginCommand);
        response.IsSuccessStatusCode.Should().BeTrue($"Login should succeed for {email}");

        var authResult = await response.Content.ReadFromJsonAsync<AuthenticationResultDto>();
        authResult.Should().NotBeNull();
        authResult!.AccessToken.Should().NotBeNullOrEmpty();

        return authResult.AccessToken;
    }

    /// <summary>
    /// Autentica un usuario y configura el header Authorization del cliente
    /// </summary>
    public static async Task AuthenticateAndSetHeaderAsync(
        HttpClient client,
        string email,
        string password = TestDataSeeder.TestPasswordPlainText)
    {
        var token = await AuthenticateAsync(client, email, password);
        SetAuthorizationHeader(client, token);
    }

    /// <summary>
    /// Configura el header Authorization con un token JWT
    /// </summary>
    public static void SetAuthorizationHeader(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Remueve el header Authorization (logout)
    /// </summary>
    public static void ClearAuthorizationHeader(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Autentica como empleador de prueba activo (juan.perez@test.com)
    /// </summary>
    public static async Task<string> AuthenticateAsEmpleadorAsync(HttpClient client)
    {
        return await AuthenticateAsync(client, "juan.perez@test.com");
    }

    /// <summary>
    /// Autentica como contratista de prueba activo (carlos.rodriguez@test.com)
    /// </summary>
    public static async Task<string> AuthenticateAsContratistaAsync(HttpClient client)
    {
        return await AuthenticateAsync(client, "carlos.rodriguez@test.com");
    }

    /// <summary>
    /// Verifica que la respuesta sea exitosa y retorna el contenido deserializado
    /// </summary>
    public static async Task<T> AssertSuccessAndGetContentAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            $"Expected success status code, but got {response.StatusCode}. Content: {await response.Content.ReadAsStringAsync()}");
        
        var content = await response.Content.ReadFromJsonAsync<T>();
        content.Should().NotBeNull();
        
        return content!;
    }

    /// <summary>
    /// Verifica que la respuesta sea un error con el status code esperado
    /// </summary>
    public static void AssertErrorStatusCode(HttpResponseMessage response, System.Net.HttpStatusCode expectedStatusCode)
    {
        response.StatusCode.Should().Be(expectedStatusCode,
            $"Expected {expectedStatusCode}, but got {response.StatusCode}");
    }
}
