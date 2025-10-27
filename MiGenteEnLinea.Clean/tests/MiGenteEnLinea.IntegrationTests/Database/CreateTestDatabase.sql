-- ================================================================================
-- Script para crear Base de Datos de Integration Tests
-- MiGente En Línea - Clean Architecture
-- ================================================================================
-- Propósito: Crear SQL Server real para tests (reemplaza InMemory Database)
-- Fecha: 2025-10-26
-- Docker: localhost,1433 | User: Sa | Password: Volumen#1
-- ================================================================================

USE master;
GO

-- Eliminar DB si existe (para recrear limpia en cada sesión de tests)
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'MiGenteTestDB')
BEGIN
    ALTER DATABASE [MiGenteTestDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [MiGenteTestDB];
    PRINT 'Database eliminada: MiGenteTestDB';
END
GO

-- Crear nueva database
CREATE DATABASE [MiGenteTestDB]
    COLLATE Modern_Spanish_CI_AS;
GO

PRINT 'Database creada: MiGenteTestDB';
GO

-- Configurar para tests
ALTER DATABASE [MiGenteTestDB] SET RECOVERY SIMPLE;
GO

USE [MiGenteTestDB];
GO

PRINT 'Database lista para EF Core Migrations.';
PRINT 'Ejecutar siguiente comando desde raíz del proyecto:';
PRINT 'dotnet ef database update --startup-project src/Presentation/MiGenteEnLinea.API --project src/Infrastructure/MiGenteEnLinea.Infrastructure --connection "Server=localhost,1433;Database=MiGenteTestDB;User Id=Sa;Password=Volumen#1;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False"';
GO
