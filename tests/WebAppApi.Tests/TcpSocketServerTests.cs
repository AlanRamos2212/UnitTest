using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebAppApi;
using Xunit;

namespace WebAppApi.Tests;

public sealed class TcpSocketFixture : IAsyncLifetime
{
    private readonly ApiFactory factory = new();
    private readonly HttpClient apiClient;
    private readonly TcpSocketServer server;

    public TcpSocketFixture()
    {
        apiClient = factory.CreateClient();
        server = new TcpSocketServer(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Services.GetRequiredService<ILogger<TcpSocketServer>>());
    }

    public async Task InitializeAsync()
    {
        await server.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await server.StopAsync(CancellationToken.None);
        server.Dispose();
        apiClient.Dispose();
        await factory.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await apiClient.DeleteAsync("/api/database");
    }

    public async Task<int> CreateCategoryAsync()
    {
        var response = await apiClient.PostAsJsonAsync("/api/categories", new { name = "TCP" });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("data").GetProperty("id").GetInt32();
    }

    public async Task<string> SendAsync(string message)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, 6061);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        await using var writer = new StreamWriter(stream) { AutoFlush = true };
        await writer.WriteLineAsync(message);
        return await reader.ReadLineAsync() ?? string.Empty;
    }
}

public sealed class TcpSocketServerTests(TcpSocketFixture fixture) : IClassFixture<TcpSocketFixture>
{
    [Fact]
    public async Task Insert_ValidProduct_ReturnsCreatedProduct()
    {
        await fixture.ResetDatabaseAsync();
        var categoryId = await fixture.CreateCategoryAsync();

        var response = await fixture.SendAsync($"{{insert:{{\"name\":\"Mouse\",\"price\":10,\"categoryId\":{categoryId}}}}}");
        using var body = JsonDocument.Parse(response);

        Assert.Equal(201, body.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("Mouse", body.RootElement.GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Get_ExistingProductById_ReturnsProduct()
    {
        await fixture.ResetDatabaseAsync();
        var categoryId = await fixture.CreateCategoryAsync();
        var insert = await fixture.SendAsync($"{{insert:{{\"name\":\"Mouse\",\"price\":10,\"categoryId\":{categoryId}}}}}");
        using var insertedBody = JsonDocument.Parse(insert);
        var productId = insertedBody.RootElement.GetProperty("data").GetProperty("id").GetInt32();

        var response = await fixture.SendAsync($"{{get:{productId}}}");
        using var body = JsonDocument.Parse(response);

        Assert.Equal(200, body.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal(productId, body.RootElement.GetProperty("data").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Get_UnknownProduct_ReturnsNotFound()
    {
        await fixture.ResetDatabaseAsync();

        var response = await fixture.SendAsync("{get:999}");
        using var body = JsonDocument.Parse(response);

        Assert.Equal(404, body.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task InvalidCommand_ReturnsBadRequest()
    {
        await fixture.ResetDatabaseAsync();

        var response = await fixture.SendAsync("invalid");
        using var body = JsonDocument.Parse(response);

        Assert.Equal(400, body.RootElement.GetProperty("statusCode").GetInt32());
    }
}
