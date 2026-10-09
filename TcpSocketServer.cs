using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebAppApi.Data;
using WebAppApi.Models;

namespace WebAppApi;

public sealed class TcpSocketServer(IServiceScopeFactory scopeFactory, ILogger<TcpSocketServer> logger) : BackgroundService
{
    private const int Port = 6061;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        logger.LogInformation("TCP socket escuchando en el puerto {Port}", Port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                _ = HandleClientAsync(client, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken stoppingToken)
    {
        using var clientScope = client;
        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            await using var writer = new StreamWriter(stream) { AutoFlush = true };
            var message = await reader.ReadLineAsync(stoppingToken);
            if (!string.IsNullOrWhiteSpace(message))
                await writer.WriteLineAsync(await ProcessMessageAsync(message));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Error procesando cliente TCP");
        }
    }

    private async Task<string> ProcessMessageAsync(string message)
    {
        try
        {
            if (message.StartsWith("{insert:", StringComparison.OrdinalIgnoreCase) && message.EndsWith('}'))
            {
                var product = JsonSerializer.Deserialize<Product>(message[8..^1], JsonOptions);
                return product is null
                    ? Error("El elemento insertado no es válido.")
                    : await InsertProductAsync(product);
            }

            if (message.StartsWith("{get:", StringComparison.OrdinalIgnoreCase) && message.EndsWith('}'))
            {
                var element = message[5..^1].Trim();
                return int.TryParse(element, out var id)
                    ? await GetProductAsync(id)
                    : await GetProductFromJsonAsync(element);
            }

            return Error("Formato inválido. Use {insert:<json>} o {get:<id>}.");
        }
        catch (JsonException)
        {
            return Error("El elemento debe ser JSON válido.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Error procesando mensaje TCP: {Message}", message);
            return Error("No se pudo procesar la solicitud.", 500);
        }
    }

    private async Task<string> InsertProductAsync(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Name) || product.Price < 0)
            return Error("Nombre y precio válido son obligatorios.");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Categories.AnyAsync(category => category.Id == product.CategoryId))
            return Error("La categoría no existe.");

        product.Id = 0;
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return JsonSerializer.Serialize(ApiResponse<Product>.Success(product, 201), JsonOptions);
    }

    private async Task<string> GetProductFromJsonAsync(string element)
    {
        using var document = JsonDocument.Parse(element);
        if (!document.RootElement.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id))
            return Error("get requiere un id numérico.");
        return await GetProductAsync(id);
    }

    private async Task<string> GetProductAsync(int id)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        return product is null
            ? Error("Producto no encontrado.", 404)
            : JsonSerializer.Serialize(ApiResponse<Product>.Success(product), JsonOptions);
    }

    private static string Error(string message, int statusCode = 400) =>
        JsonSerializer.Serialize(ApiResponse<object>.Error(message, statusCode), JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}