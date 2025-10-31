using System.Text.Json.Serialization;

namespace MiGenteEnLinea.Application.Features.Authentication.DTOs;

/// <summary>
/// Resultado de la operación de registro
/// </summary>
public class RegisterResult
{
    /// <summary>
    /// Indica si el registro fue exitoso
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensaje de resultado
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// ID del usuario creado (Legacy Credenciales table - int ID)
    /// Este es el ID entero de la tabla Credenciales para compatibilidad con tests y sistema Legacy
    /// Serializado como "userId" para mantener compatibilidad con tests existentes
    /// </summary>
    [JsonPropertyName("userId")]
    public int? CredentialId { get; set; }

    /// <summary>
    /// ID del usuario en Identity (GUID) - Primary Key del sistema
    /// Usado para CreatedAtAction routing en AuthController
    /// Serializado como "identityUserId" para evitar colisión con "userId"
    /// </summary>
    [JsonPropertyName("identityUserId")]
    public string? IdentityUserId { get; set; }

    /// <summary>
    /// Email del usuario creado
    /// </summary>
    public string? Email { get; set; }
}
