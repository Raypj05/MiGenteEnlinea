using Microsoft.AspNetCore.Identity;
using MiGenteEnLinea.Infrastructure.Identity;
using Xunit;
using Xunit.Abstractions;

namespace MiGenteEnLinea.IntegrationTests.Utilities;

public class HashGeneratorTest
{
    private readonly ITestOutputHelper _output;

    public HashGeneratorTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GenerateIdentityHash()
    {
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var user = new ApplicationUser { UserName = "test@test.com" };
        var hash = passwordHasher.HashPassword(user, "Test1234!");
        
        _output.WriteLine($"IDENTITY_HASH={hash}");
        _output.WriteLine($"LENGTH={hash.Length}");
        
        // Este test siempre pasa, solo imprime el hash
        Assert.NotEmpty(hash);
    }
}
