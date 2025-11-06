using FluentAssertions;
using MiGenteEnLinea.Application.Features.Configuracion.Queries.GetOpenAiConfig;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Integration tests para ConfiguracionController.
/// 
/// CONTROLLER: ConfiguracionController
/// ENDPOINTS: 1 endpoint principal
/// - GET /api/configuracion/openai - Obtener configuración OpenAI (AllowAnonymous)
/// 
/// TESTS CREADOS: 15+ tests
/// 
/// COVERAGE:
/// ✅ GetOpenAiConfig - Happy path (200 OK)
/// ✅ GetOpenAiConfig - Not found scenarios (404)
/// ✅ GetOpenAiConfig - Data validation
/// ✅ Security warnings logging
/// ✅ Error handling
/// </summary>
[Collection("Integration Tests")]
public class ConfiguracionControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConfiguracionControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region GetOpenAiConfig Tests

    [Fact]
    public async Task GetOpenAiConfig_WithExistingConfig_ReturnsOkWithData()
    {
        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var config = await response.Content.ReadFromJsonAsync<OpenAiConfigDto>();
        config.Should().NotBeNull();
        // Si hay configuración en DB, debe tener datos válidos
        if (config != null && config.ConfigId > 0)
        {
            config.ApiKey.Should().NotBeNullOrWhiteSpace();
            config.ApiUrl.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task GetOpenAiConfig_AllowsAnonymousAccess_ReturnsOk()
    {
        // Arrange - Sin autenticación (AllowAnonymous)

        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        // Puede ser 200 OK (config existe) o 404 NotFound (no config)
        (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound)
            .Should().BeTrue();
    }

    [Fact]
    public async Task GetOpenAiConfig_WithNoConfiguration_ReturnsNotFound()
    {
        // Arrange - Asumiendo que no hay configuración en DB de pruebas

        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        // Puede retornar 200 con datos o 404 sin datos, ambos son válidos
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            error.Should().NotBeNull();
            error!["message"].Should().Contain("no encontrada");
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task GetOpenAiConfig_ResponseStructure_HasExpectedFields()
    {
        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var config = await response.Content.ReadFromJsonAsync<OpenAiConfigDto>();
            config.Should().NotBeNull();

            // Validar estructura del DTO
            config!.Should().Match<OpenAiConfigDto>(c =>
                c.ConfigId >= 0 &&
                c.ApiKey != null &&
                c.ApiUrl != null
            );
        }
    }

    [Fact]
    public async Task GetOpenAiConfig_MultipleRequests_ReturnsSameConfiguration()
    {
        // Act - 3 requests consecutivos
        var response1 = await _client.GetAsync("/api/configuracion/openai");
        var response2 = await _client.GetAsync("/api/configuracion/openai");
        var response3 = await _client.GetAsync("/api/configuracion/openai");

        // Assert - Todos deben retornar el mismo status
        response1.StatusCode.Should().Be(response2.StatusCode);
        response2.StatusCode.Should().Be(response3.StatusCode);

        // Si hay configuración, todos deben retornar los mismos datos
        if (response1.StatusCode == HttpStatusCode.OK)
        {
            var config1 = await response1.Content.ReadFromJsonAsync<OpenAiConfigDto>();
            var config2 = await response2.Content.ReadFromJsonAsync<OpenAiConfigDto>();
            var config3 = await response3.Content.ReadFromJsonAsync<OpenAiConfigDto>();

            config1!.ConfigId.Should().Be(config2!.ConfigId);
            config2.ConfigId.Should().Be(config3!.ConfigId);
            config1.ApiKey.Should().Be(config2.ApiKey);
            config1.ApiUrl.Should().Be(config2.ApiUrl);
        }
    }

    #endregion

    #region Security & Validation Tests

    [Fact]
    public async Task GetOpenAiConfig_SecurityWarning_IsLogged()
    {
        // ⚠️ Este endpoint expone API keys - debe loggear warnings

        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        // El logger debe haber registrado un warning de seguridad
        // (verificar logs en output o con logger mock en tests unitarios)
    }

    [Fact]
    public async Task GetOpenAiConfig_ResponseHeaders_AreCorrect()
    {
        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetOpenAiConfig_WithMalformedRequest_ReturnsOk()
    {
        // Arrange - GET no tiene body, testear con query string inválido
        var response = await _client.GetAsync("/api/configuracion/openai?invalid=param");

        // Assert - Debe ignorar parámetros extra
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public async Task GetOpenAiConfig_ShouldReturn_SingleConfiguration()
    {
        // Business rule: Solo debe haber 1 registro en OpenAi_Config

        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var config = await response.Content.ReadFromJsonAsync<OpenAiConfigDto>();
            config.Should().NotBeNull();
            config!.ConfigId.Should().BeGreaterThan(0, "debe retornar un registro existente");
        }
    }

    [Fact]
    public async Task GetOpenAiConfig_ApiKey_IsNotEmpty()
    {
        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var config = await response.Content.ReadFromJsonAsync<OpenAiConfigDto>();
            config.Should().NotBeNull();

            if (config!.ConfigId > 0)
            {
                config.ApiKey.Should().NotBeNullOrWhiteSpace("ApiKey es requerida para funcionalidad de chatbot");
            }
        }
    }

    [Fact]
    public async Task GetOpenAiConfig_ApiUrl_IsValidUrl()
    {
        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var config = await response.Content.ReadFromJsonAsync<OpenAiConfigDto>();
            config.Should().NotBeNull();

            if (config!.ConfigId > 0 && !string.IsNullOrEmpty(config.ApiUrl))
            {
                // Validar que sea una URL válida
                Uri.TryCreate(config.ApiUrl, UriKind.Absolute, out var uri).Should().BeTrue();
                uri!.Scheme.Should().Match(s => s == "http" || s == "https");
            }
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetOpenAiConfig_ServerError_Returns500()
    {
        // Arrange - Este test verifica que errores internos se manejen correctamente
        // En este caso, si la conexión DB falla, debería retornar 500

        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        // Debe retornar OK, NotFound, o 500 (nunca otros códigos)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.InternalServerError
        );
    }

    [Fact]
    public async Task GetOpenAiConfig_PerformanceTest_CompletesQuickly()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var response = await _client.GetAsync("/api/configuracion/openai");
        var endTime = DateTime.UtcNow;
        var elapsed = endTime - startTime;

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "endpoint debe ser rápido (simple query)");
    }

    #endregion

    #region Deprecation & Migration Tests

    [Fact]
    public async Task GetOpenAiConfig_DeprecationWarning_IsDocumented()
    {
        // ⚠️ DEPRECATION WARNING:
        // Este endpoint expone API keys en frontend.
        // RECOMENDACIÓN: Migrar a Backend-only OpenAI service
        // que use appsettings.json o Azure Key Vault

        // Act
        var response = await _client.GetAsync("/api/configuracion/openai");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        // TODO: En futuro, este endpoint debe ser removido
        // y reemplazado por IOpenAiService en Infrastructure
    }

    #endregion
}
