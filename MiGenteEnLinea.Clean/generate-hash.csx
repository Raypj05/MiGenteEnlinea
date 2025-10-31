using Microsoft.AspNetCore.Identity;

var passwordHasher = new PasswordHasher<IdentityUser>();
var user = new IdentityUser { UserName = "test@test.com" };
var hash = passwordHasher.HashPassword(user, "Test1234!");

Console.WriteLine($"GENERATED_HASH={hash}");
