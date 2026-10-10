using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace WebAppApi.Tests;

public sealed class ApiEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task GetHealth_WhenDatabaseIsAvailable_ReturnsHealthy()
    {
        var response = await client.GetAsync("/health");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy2", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.RootElement.GetProperty("database").GetString());
    }

    [Fact]
    public async Task GetCategories_Empty_Returns200()
    {
        await factory.ResetDatabaseAsync();
        var response = await client.GetAsync("/api/categories");
        var body = await ReadBodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(200, body.GetProperty("statusCode").GetInt32());
        Assert.Empty(body.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task GetCategories_AfterInsert_ReturnsCategory()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        var body = await ReadBodyAsync(await client.GetAsync("/api/categories"));
        Assert.Single(body.GetProperty("data").EnumerateArray());
        Assert.Equal("Pruebas", body.GetProperty("data")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetCategories_HasStandardContract()
    {
        await factory.ResetDatabaseAsync();
        var body = await ReadBodyAsync(await client.GetAsync("/api/categories"));
        Assert.True(body.TryGetProperty("statusCode", out _));
        Assert.True(body.TryGetProperty("data", out _));
        Assert.True(body.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task PostCategory_Valid_Returns201()
    {
        await factory.ResetDatabaseAsync();
        var response = await client.PostAsJsonAsync("/api/categories", new { name = "Pruebas" });
        var body = await ReadBodyAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(201, body.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task PostCategory_EmptyName_Returns400()
    {
        await factory.ResetDatabaseAsync();
        var response = await client.PostAsJsonAsync("/api/categories", new { name = " " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCategory_MissingName_Returns400()
    {
        await factory.ResetDatabaseAsync();
        var response = await client.PostAsJsonAsync("/api/categories", new { description = "Sin nombre" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_ExistingWithoutProducts_Returns200()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        Assert.Equal(HttpStatusCode.OK, await DeleteAsync("/api/categories/1"));
    }

    [Fact]
    public async Task DeleteCategory_Unknown_Returns404()
    {
        await factory.ResetDatabaseAsync();
        var response = await client.DeleteAsync("/api/categories/999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithProducts_Returns409()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        await CreateProductAsync();
        Assert.Equal(HttpStatusCode.Conflict, await DeleteAsync("/api/categories/1"));
    }

    [Fact]
    public async Task GetProducts_Empty_ReturnsEmptyArray()
    {
        await factory.ResetDatabaseAsync();
        var body = await ReadBodyAsync(await client.GetAsync("/api/products"));
        Assert.Equal(200, body.GetProperty("statusCode").GetInt32());
        Assert.Empty(body.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task GetProducts_AfterInsert_ReturnsProduct()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        await CreateProductAsync();
        var body = await ReadBodyAsync(await client.GetAsync("/api/products"));
        Assert.Equal("Producto de prueba", body.GetProperty("data")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetProducts_HasStandardContract()
    {
        await factory.ResetDatabaseAsync();
        var body = await ReadBodyAsync(await client.GetAsync("/api/products"));
        Assert.True(body.TryGetProperty("statusCode", out _));
        Assert.True(body.TryGetProperty("data", out _));
        Assert.True(body.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task PostProduct_Valid_Returns201()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        var response = await client.PostAsJsonAsync("/api/products", ProductRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_UnknownCategory_Returns400()
    {
        await factory.ResetDatabaseAsync();
        var response = await client.PostAsJsonAsync("/api/products", ProductRequest(categoryId: 999));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_NegativePrice_Returns400()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        var response = await client.PostAsJsonAsync("/api/products", ProductRequest(price: -1));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProduct_Existing_ReturnsProduct()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        await CreateProductAsync();
        var body = await ReadBodyAsync(await client.GetAsync("/api/products/1"));
        Assert.Equal(1, body.GetProperty("data").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task GetProduct_Unknown_Returns404()
    {
        await factory.ResetDatabaseAsync();
        Assert.Equal(HttpStatusCode.NotFound, await GetStatusAsync("/api/products/999"));
    }

    [Fact]
    public async Task GetProduct_NegativeId_Returns404()
    {
        await factory.ResetDatabaseAsync();
        Assert.Equal(HttpStatusCode.NotFound, await GetStatusAsync("/api/products/-1"));
    }

    [Fact]
    public async Task PutProduct_Valid_ReturnsUpdatedProduct()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        await CreateProductAsync();
        var response = await client.PutAsJsonAsync("/api/products/1", ProductRequest(name: "Actualizado"));
        var body = await ReadBodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Actualizado", body.GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task PutProduct_UnknownId_Returns404()
    {
        await factory.ResetDatabaseAsync();
        Assert.Equal(HttpStatusCode.NotFound, await PutStatusAsync("/api/products/999", ProductRequest()));
    }

    [Fact]
    public async Task PutProduct_InvalidCategory_Returns400()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        await CreateProductAsync();
        Assert.Equal(HttpStatusCode.BadRequest, await PutStatusAsync("/api/products/1", ProductRequest(categoryId: 999)));
    }

    [Fact]
    public async Task DeleteProduct_Existing_Returns200()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        await CreateProductAsync();
        Assert.Equal(HttpStatusCode.OK, await DeleteAsync("/api/products/1"));
    }

    [Fact]
    public async Task DeleteProduct_Unknown_Returns404()
    {
        await factory.ResetDatabaseAsync();
        Assert.Equal(HttpStatusCode.NotFound, await DeleteAsync("/api/products/999"));
    }

    [Fact]
    public async Task DeleteProduct_AfterDelete_CannotBeFound()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        await CreateProductAsync();
        await client.DeleteAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.NotFound, await GetStatusAsync("/api/products/1"));
    }

    [Fact]
    public async Task CreateBackup_Returns200()
    {
        await factory.ResetDatabaseAsync();
        Assert.Equal(HttpStatusCode.OK, await PostStatusAsync("/api/database/backup"));
    }

    [Fact]
    public async Task CreateBackup_ReturnsDbFileName()
    {
        await factory.ResetDatabaseAsync();
        var body = await ReadBodyAsync(await client.PostAsync("/api/database/backup", null));
        Assert.EndsWith(".db", body.GetProperty("data").GetProperty("file").GetString());
    }

    [Fact]
    public async Task CreateBackup_CanBeCalledTwice()
    {
        await factory.ResetDatabaseAsync();
        Assert.Equal(HttpStatusCode.OK, await PostStatusAsync("/api/database/backup"));
        Assert.Equal(HttpStatusCode.OK, await PostStatusAsync("/api/database/backup"));
    }

    [Fact]
    public async Task DownloadBackup_Returns200()
    {
        await factory.ResetDatabaseAsync();
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync("/api/database/backup/download"));
    }

    [Fact]
    public async Task DownloadBackup_ReturnsBinaryContent()
    {
        await factory.ResetDatabaseAsync();
        var response = await client.GetAsync("/api/database/backup/download");
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task DownloadBackup_ReturnsDbFileName()
    {
        await factory.ResetDatabaseAsync();
        var response = await client.GetAsync("/api/database/backup/download");
        Assert.Contains(".db", response.Content.Headers.ContentDisposition?.FileName ?? string.Empty);
    }

    [Fact]
    public async Task ClearDatabase_Returns200()
    {
        await factory.ResetDatabaseAsync();
        Assert.Equal(HttpStatusCode.OK, await DeleteAsync("/api/database"));
    }

    [Fact]
    public async Task ClearDatabase_RemovesProducts()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        await CreateProductAsync();
        await client.DeleteAsync("/api/database");
        var body = await ReadBodyAsync(await client.GetAsync("/api/products"));
        Assert.Empty(body.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task ClearDatabase_RemovesCategories()
    {
        await factory.ResetDatabaseAsync();
        await CreateCategoryAsync();
        await client.DeleteAsync("/api/database");
        var body = await ReadBodyAsync(await client.GetAsync("/api/categories"));
        Assert.Empty(body.GetProperty("data").EnumerateArray());
    }

    private async Task CreateCategoryAsync()
    {
        var response = await client.PostAsJsonAsync("/api/categories", new { name = "Pruebas" });
        response.EnsureSuccessStatusCode();
    }

    private async Task CreateProductAsync()
    {
        var response = await client.PostAsJsonAsync("/api/products", ProductRequest());
        response.EnsureSuccessStatusCode();
    }

    private static object ProductRequest(string name = "Producto de prueba", decimal price = 100, int categoryId = 1) => new
    {
        name,
        description = "Producto para pruebas",
        price,
        categoryId
    };

    private async Task<HttpStatusCode> GetStatusAsync(string route) => (await client.GetAsync(route)).StatusCode;
    private async Task<HttpStatusCode> DeleteAsync(string route) => (await client.DeleteAsync(route)).StatusCode;
    private async Task<HttpStatusCode> PostStatusAsync(string route) => (await client.PostAsync(route, null)).StatusCode;
    private async Task<HttpStatusCode> PutStatusAsync(string route, object body) => (await client.PutAsJsonAsync(route, body)).StatusCode;

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
