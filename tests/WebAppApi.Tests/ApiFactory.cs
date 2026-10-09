using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebAppApi.Data;

namespace WebAppApi.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly string contentRoot = Path.Combine(Path.GetTempPath(), $"WebAppApi.Tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        Directory.CreateDirectory(Path.Combine(contentRoot, "data"));
        File.WriteAllBytes(Path.Combine(contentRoot, "data", "webapp.db"), "SQLite test backup"u8.ToArray());
        builder.UseEnvironment("Testing");
        builder.UseSetting(WebHostDefaults.ContentRootKey, contentRoot);
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.Single(service => service.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(dbContextDescriptor);

            var hostedServices = services.Where(service =>
                service.ServiceType == typeof(IHostedService) &&
                service.ImplementationType == typeof(TcpSocketServer)).ToList();
            foreach (var hostedService in hostedServices)
                services.Remove(hostedService);

            services.AddSingleton<DbConnection>(connection);
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Products;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Categories;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence;");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            connection.Dispose();
            if (Directory.Exists(contentRoot))
                Directory.Delete(contentRoot, true);
        }
        base.Dispose(disposing);
    }
}