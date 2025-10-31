using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using MiGenteEnLinea.Infrastructure.Identity;

#nullable disable

namespace MiGenteEnLinea.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración para actualizar los hashes de contraseña de BCrypt a ASP.NET Core Identity PasswordHasher
    /// para usuarios de prueba (test.com)
    /// </summary>
    public partial class RehashTestUsersToIdentity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Esta migración actualiza los hashes de contraseña de usuarios de prueba
            // de BCrypt a Identity PasswordHasher format
            
            // Password: Test1234!
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var tempUser = new ApplicationUser { UserName = "test@test.com" };
            var identityHash = passwordHasher.HashPassword(tempUser, "Test1234!");
            
            // Actualizar TODOS los usuarios con @test.com (incluyendo empleador_ y contratista_)
            // Esto incluye usuarios seedeados y usuarios generados dinámicamente en tests
            migrationBuilder.Sql($@"
                UPDATE Credenciales 
                SET password = '{identityHash}'
                WHERE email LIKE '%@test.com'
            ");
            
            // Log: Actualiza juan.perez@test.com, maria.garcia@test.com, ana.martinez@test.com, 
            // carlos.lopez@test.com, empleador_*@test.com, contratista_*@test.com, etc.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No podemos revertir a BCrypt porque perdimos el salt original
            // Esta migración es irreversible
            // Los usuarios tendrán que usar "Test1234!" como contraseña
        }
    }
}
