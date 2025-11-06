using FluentAssertions;
using MiGenteEnLinea.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace MiGenteEnLinea.IntegrationTests.Controllers;

/// <summary>
/// Integration tests para UtilitariosController.
/// 
/// CONTROLLER: UtilitariosController
/// ENDPOINTS: 1 endpoint principal
/// - GET /api/utilitarios/numero-a-letras - Convierte decimal a texto en español (GAP-020)
/// 
/// TESTS CREADOS: 20+ tests
/// 
/// COVERAGE:
/// ✅ Conversión básica con y sin moneda
/// ✅ Edge cases: cero, negativos, números grandes
/// ✅ Validación de rango (0 a 999,999,999,999,999)
/// ✅ Formato de texto español (mayúsculas, decimales XX/100)
/// ✅ Performance tests
/// ✅ Casos de uso reales (nómina, contratos, recibos)
/// 
/// BUSINESS CONTEXT:
/// - Usado en documentos legales, recibos de pago, contratos
/// - Legacy: NumeroEnLetras.cs extension method
/// - Formato legal: "MIL DOSCIENTOS CINCUENTA PESOS DOMINICANOS 50/100"
/// </summary>
[Collection("Integration Tests")]
public class UtilitariosControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UtilitariosControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Basic Conversion Tests

    [Fact]
    public async Task NumeroALetras_ConMoneda_ReturnsCorrectFormat()
    {
        // Arrange
        var numero = 1250.50m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        resultado.Should().NotBeNull();
        resultado!.Should().ContainKey("texto");

        var texto = resultado["texto"];
        texto.Should().Contain("MIL DOSCIENTOS CINCUENTA");
        texto.Should().Contain("PESOS DOMINICANOS");
        texto.Should().Contain("50/100");
    }

    [Fact]
    public async Task NumeroALetras_SinMoneda_ReturnsNumberOnly()
    {
        // Arrange
        var numero = 123m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        resultado.Should().NotBeNull();

        var texto = resultado!["texto"];
        texto.Should().Be("CIENTO VEINTITRES");
        texto.Should().NotContain("PESOS");
        texto.Should().NotContain("/100");
    }

    [Fact]
    public async Task NumeroALetras_Cero_ReturnsCorrectText()
    {
        // Arrange
        var numero = 0m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Be("CERO");
    }

    [Fact]
    public async Task NumeroALetras_CeroConMoneda_ReturnsCorrectFormat()
    {
        // Arrange
        var numero = 0m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Contain("CERO");
        texto.Should().Contain("PESOS DOMINICANOS");
        texto.Should().Contain("00/100");
    }

    #endregion

    #region Edge Cases - Large Numbers

    [Fact]
    public async Task NumeroALetras_Millones_ReturnsCorrectText()
    {
        // Arrange - 5 millones
        var numero = 5000000m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Contain("MILLONES");
    }

    [Fact]
    public async Task NumeroALetras_Billones_ReturnsCorrectText()
    {
        // Arrange - 2 billones (trillion en inglés)
        var numero = 2000000000000m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Contain("BILLONES");
    }

    [Fact]
    public async Task NumeroALetras_MaximoPermitido_ReturnsCorrectText()
    {
        // Arrange - Máximo: 999,999,999,999,999
        var numero = 999999999999999m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        resultado.Should().NotBeNull();
        resultado!["texto"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NumeroALetras_FueraDeRango_ReturnsBadRequest()
    {
        // Arrange - Excede rango máximo
        var numero = 1000000000000000m; // 1 cuadrillón (fuera de rango)

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Decimal Handling Tests

    [Fact]
    public async Task NumeroALetras_ConDecimales_FormateaCentavosCorrectamente()
    {
        // Arrange
        var numero = 1500.75m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Contain("75/100", "centavos deben mostrarse como fracción");
    }

    [Fact]
    public async Task NumeroALetras_ConUnCentavo_FormateaCorrectamente()
    {
        // Arrange
        var numero = 1000.01m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Contain("01/100");
    }

    [Fact]
    public async Task NumeroALetras_SinDecimales_MuestraDosCeros()
    {
        // Arrange
        var numero = 5000m; // Sin decimales

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Contain("00/100");
    }

    #endregion

    #region Format Validation Tests

    [Fact]
    public async Task NumeroALetras_TextoEnMayusculas_Siempre()
    {
        // Arrange
        var numero = 456.78m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Be(texto.ToUpper(), "todo el texto debe estar en mayúsculas");
    }

    [Fact]
    public async Task NumeroALetras_ResponseFormat_EsJson()
    {
        // Arrange
        var numero = 100m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    #endregion

    #region Business Use Cases

    [Fact]
    public async Task NumeroALetras_SalarioNomina_FormateaCorrectamente()
    {
        // Use case: Salario mensual en recibo de nómina
        // Ejemplo: RD$ 45,000.00

        // Arrange
        var salario = 45000.00m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={salario}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Contain("CUARENTA Y CINCO MIL");
        texto.Should().Contain("PESOS DOMINICANOS");
        texto.Should().Contain("00/100");
    }

    [Fact]
    public async Task NumeroALetras_MontoContrato_FormateaCorrectamente()
    {
        // Use case: Monto de contrato legal
        // Ejemplo: RD$ 150,000.50

        // Arrange
        var montoContrato = 150000.50m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={montoContrato}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Contain("CIENTO CINCUENTA MIL");
        texto.Should().Contain("50/100");
    }

    [Fact]
    public async Task NumeroALetras_PrestacionesLaborales_FormateaCorrectamente()
    {
        // Use case: Prestaciones laborales en liquidación
        // Ejemplo: RD$ 87,345.25

        // Arrange
        var prestaciones = 87345.25m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={prestaciones}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().Contain("OCHENTA Y SIETE MIL");
        texto.Should().Contain("25/100");
    }

    #endregion

    #region Parameter Validation Tests

    [Fact]
    public async Task NumeroALetras_SinParametroNumero_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/utilitarios/numero-a-letras?incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NumeroALetras_NumeroNegativo_ReturnsBadRequest()
    {
        // Arrange
        var numero = -1000m;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NumeroALetras_DefaultIncluirMoneda_EsFalse()
    {
        // Arrange
        var numero = 500m;

        // Act - Sin especificar incluirMoneda
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var texto = resultado!["texto"];
        texto.Should().NotContain("PESOS", "default debería ser sin moneda");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task NumeroALetras_RespondeRapidamente()
    {
        // Arrange
        var numero = 12345.67m;
        var startTime = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");

        // Assert
        var elapsed = DateTime.UtcNow - startTime;
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "conversión debe ser rápida");
    }

    [Fact]
    public async Task NumeroALetras_MultipleRequests_RetornanResultadosConsistentes()
    {
        // Arrange
        var numero = 7500.25m;

        // Act - 3 requests consecutivos
        var response1 = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");
        var response2 = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");
        var response3 = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={numero}&incluirMoneda=true");

        // Assert
        var resultado1 = await response1.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var resultado2 = await response2.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var resultado3 = await response3.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        resultado1!["texto"].Should().Be(resultado2!["texto"]);
        resultado2["texto"].Should().Be(resultado3!["texto"]);
    }

    #endregion

    #region Legacy Compatibility Tests

    [Fact]
    public async Task NumeroALetras_GAP020Implementation_EsConsistenteConLegacy()
    {
        // GAP-020: Implementación de NumeroEnLetras.cs legacy

        // Arrange - Casos de prueba del legacy
        var casosPrueba = new Dictionary<decimal, string>
        {
            { 1m, "UNO" },
            { 15m, "QUINCE" },
            { 100m, "CIEN" },
            { 101m, "CIENTO UNO" },
            { 1000m, "MIL" },
            { 1001m, "MIL UNO" }
        };

        foreach (var caso in casosPrueba)
        {
            // Act
            var response = await _client.GetAsync($"/api/utilitarios/numero-a-letras?numero={caso.Key}&incluirMoneda=false");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            resultado!["texto"].Should().Be(caso.Value, $"conversión de {caso.Key} debe ser consistente con legacy");
        }
    }

    #endregion
}
