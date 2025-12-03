using PdfViewrMiniPr.Domain.Entities;
using PdfViewrMiniPr.Domain.Enums;
using PdfViewrMiniPr.Infrastructure.Database;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewerMiniPr;

public static class SeedData
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure database is created
        context.Database.EnsureCreated();

        // Seed admin user if none exists
        if (!context.Users.Any())
        {
            var adminUser = new User
            {
                Email = "admin@company.com",
                FullName = "Administrator",
                Role = UserRole.Admin,
                PasswordHash = HashPassword("Admin123!"),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var internalUser1 = new User
            {
                Email = "internal1@company.com",
                FullName = "Internal Reviewer 1",
                Role = UserRole.InternalUser,
                PasswordHash = HashPassword("Internal123!"),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var internalUser2 = new User
            {
                Email = "internal2@company.com",
                FullName = "Internal Reviewer 2",
                Role = UserRole.InternalUser,
                PasswordHash = HashPassword("Internal123!"),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var externalUser = new User
            {
                Email = "external@client.com",
                FullName = "External Reviewer",
                Role = UserRole.ExternalUser,
                PasswordHash = HashPassword("External123!"),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            context.Users.AddRange(adminUser, internalUser1, internalUser2, externalUser);
            context.SaveChanges();

        
        }
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

