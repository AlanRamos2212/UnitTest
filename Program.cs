using Microsoft.EntityFrameworkCore;
using WebAppApi;
using WebAppApi.Data;
using WebAppApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=data/webapp.db"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<TcpSocketServer>();
builder.Services.AddCors(options => options.AddPolicy("all", policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "data"));
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("all");

app.MapGet("/api/categories", async (AppDbContext db) =>
    Results.Ok(ApiResponse<List<Category>>.Success(await db.Categories.AsNoTracking().ToListAsync())));

app.MapPost("/api/categories", async (Category category, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(category.Name))
        return Results.BadRequest(ApiResponse<object>.Error("El nombre es obligatorio."));
    category.Id = 0;
    db.Categories.Add(category);
    await db.SaveChangesAsync();
    return Results.Created($"/api/categories/{category.Id}", ApiResponse<Category>.Success(category, 201));
});

app.MapDelete("/api/categories/{id:int}", async (int id, AppDbContext db) =>
{
    var category = await db.Categories.FindAsync(id);
    if (category is null) return Results.NotFound(ApiResponse<object>.Error("Categoría no encontrada.", 404));
    if (await db.Products.AnyAsync(product => product.CategoryId == id))
        return Results.Conflict(ApiResponse<object>.Error("No se puede eliminar una categoría con productos.", 409));
    db.Categories.Remove(category);
    await db.SaveChangesAsync();
    return Results.Ok(ApiResponse<object>.Success(null));
});

app.MapGet("/api/products", async (AppDbContext db) =>
    Results.Ok(ApiResponse<List<Product>>.Success(await db.Products.AsNoTracking().ToListAsync())));

app.MapPost("/api/products", async (Product product, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(product.Name) || product.Price < 0)
        return Results.BadRequest(ApiResponse<object>.Error("Nombre y precio válido son obligatorios."));
    if (!await db.Categories.AnyAsync(category => category.Id == product.CategoryId))
        return Results.BadRequest(ApiResponse<object>.Error("La categoría no existe."));
    product.Id = 0;
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/api/products/{product.Id}", ApiResponse<Product>.Success(product, 201));
});

app.MapGet("/api/products/{id:int}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
    return product is null
        ? Results.NotFound(ApiResponse<object>.Error("Producto no encontrado.", 404))
        : Results.Ok(ApiResponse<Product>.Success(product));
});

app.MapPut("/api/products/{id:int}", async (int id, Product input, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound(ApiResponse<object>.Error("Producto no encontrado.", 404));
    if (string.IsNullOrWhiteSpace(input.Name) || input.Price < 0)
        return Results.BadRequest(ApiResponse<object>.Error("Nombre y precio válido son obligatorios."));
    if (!await db.Categories.AnyAsync(category => category.Id == input.CategoryId))
        return Results.BadRequest(ApiResponse<object>.Error("La categoría no existe."));
    product.Name = input.Name;
    product.Description = input.Description;
    product.Price = input.Price;
    product.CategoryId = input.CategoryId;
    await db.SaveChangesAsync();
    return Results.Ok(ApiResponse<Product>.Success(product));
});

app.MapDelete("/api/products/{id:int}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound(ApiResponse<object>.Error("Producto no encontrado.", 404));
    db.Products.Remove(product);
    await db.SaveChangesAsync();
    return Results.Ok(ApiResponse<object>.Success(null));
});

app.MapPost("/api/database/backup", (IConfiguration configuration, IWebHostEnvironment environment) =>
{
    var source = Path.Combine(environment.ContentRootPath, "data", "webapp.db");
    if (!File.Exists(source)) return Results.NotFound(ApiResponse<object>.Error("La base de datos aún no existe."));
    var backupDirectory = Path.Combine(environment.ContentRootPath, "backups");
    Directory.CreateDirectory(backupDirectory);
    var backupName = $"webapp-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db";
    File.Copy(source, Path.Combine(backupDirectory, backupName), true);
    return Results.Ok(ApiResponse<object>.Success(new { file = backupName }));
});

app.MapGet("/api/database/backup/download", async (AppDbContext db, IWebHostEnvironment environment) =>
{
    var source = Path.Combine(environment.ContentRootPath, "data", "webapp.db");
    if (!File.Exists(source)) return Results.NotFound(ApiResponse<object>.Error("La base de datos aún no existe.", 404));
    await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");
    var backupName = $"webapp-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db";
    return Results.File(await File.ReadAllBytesAsync(source), "application/octet-stream", backupName);
});

app.MapDelete("/api/database", async (AppDbContext db) =>
{
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Products;");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM Categories;");
    return Results.Ok(ApiResponse<object>.Success(new { message = "Base de datos vaciada." }));
});

app.Run();

public partial class Program { }

public record ApiResponse<T>(int statusCode, T? data, string? error = null)
{
    public static ApiResponse<T> Success(T? data, int statusCode = 200) => new(statusCode, data);
    public static ApiResponse<T> Error(string message, int statusCode = 400) => new(statusCode, default, message);
}
