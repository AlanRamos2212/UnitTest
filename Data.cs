using Microsoft.EntityFrameworkCore;
using WebAppApi.Models;

namespace WebAppApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasKey(category => category.Id);
        modelBuilder.Entity<Category>().HasIndex(category => category.Name).IsUnique();
        modelBuilder.Entity<Product>().HasKey(product => product.Id);
        modelBuilder.Entity<Product>().Property(product => product.Price).HasPrecision(18, 2);
        modelBuilder.Entity<Product>()
            .HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
