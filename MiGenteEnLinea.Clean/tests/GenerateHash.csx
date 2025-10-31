#!/usr/bin/env dotnet-script
#r "nuget: BCrypt.Net-Next, 4.0.3"

using BCrypt.Net;

// Generate hash for Test@1234
string password = "Test@1234";
string hash = BCrypt.Net.BCrypt.HashPassword(password, 12);

Console.WriteLine($"Password: {password}");
Console.WriteLine($"Hash: {hash}");

// Verify it works
bool isValid = BCrypt.Net.BCrypt.Verify(password, hash);
Console.WriteLine($"Verification: {(isValid ? "✅ VALID" : "❌ INVALID")}");
