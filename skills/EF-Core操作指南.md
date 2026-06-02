# EF Core 操作指南

## 常用指令

```bash
# 新增 Migration
dotnet ef migrations add {MigrationName} --project ReactL.api

# 套用到資料庫
dotnet ef database update --project ReactL.api

# 回滾到指定 Migration
dotnet ef database update {MigrationName} --project ReactL.api

# 查看 Migration 清單
dotnet ef migrations list --project ReactL.api

# 移除最後一個尚未套用的 Migration
dotnet ef migrations remove --project ReactL.api
```

## 新增 Entity 步驟

1. 在 `Models/` 建立 Entity 類別，繼承 `BaseEntity`
2. 在 `Data/AppDbContext.cs` 新增 `DbSet<T>`
3. 執行 `dotnet ef migrations add Add{EntityName}`
4. 執行 `dotnet ef database update`
5. 若有初始資料，在 `SqlScripts/Init/` 新增對應 Seed SQL

## Code-First 實體規範

```csharp
public class Persona : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    // 導航屬性
    public ICollection<Conversation> Conversations { get; set; } = [];
}

// BaseEntity（所有 Entity 繼承）
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

## DbContext 設定

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Persona> Personas => Set<Persona>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fluent API 設定放這裡
        modelBuilder.Entity<Persona>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
        });
    }
}
```
