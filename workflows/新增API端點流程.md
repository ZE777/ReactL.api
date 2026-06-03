# 新增 API 端點流程

每次新增一個資源的 CRUD，依照以下順序進行。

---

## 步驟一：建立 Entity

`Models/Personas/Persona.cs`
```csharp
public class Persona : SoftDeletableEntity
{
    /// <summary>建立此角色的使用者</summary>
    /// <remarks>uniqueidentifier · NOT NULL · FK → Users</remarks>
    public Guid UserId { get; set; }

    /// <summary>角色名稱</summary>
    /// <remarks>nvarchar(100) · NOT NULL</remarks>
    public string Name { get; set; } = string.Empty;

    // 其他欄位...

    public User User { get; set; } = null!;
}
```

**規範**：所有欄位都要有 XML `<summary>`（用途）和 `<remarks>`（SQL 型別 · 限制），繼承基底選 `BaseEntity` / `AuditableEntity` / `SoftDeletableEntity` 之一。

---

## 步驟二：更新 DbContext

`Data/AppDbContext.cs`
```csharp
public DbSet<Persona> Personas => Set<Persona>();
```

若需要 Fluent API（軟刪除全域篩選、索引、外鍵行為），在 `OnModelCreating` 補充：
```csharp
modelBuilder.Entity<Persona>(e => {
    e.HasQueryFilter(p => !p.IsDeleted);
    e.HasIndex(p => p.UserId);
});
```

---

## 步驟三：建立 Domain 物件

`Domain/Personas/PersonaDomain.cs`
```csharp
namespace ReactL.api.Domain.Personas;

/// <summary>Persona 業務物件，Service 回傳型別</summary>
public class PersonaDomain
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    // 其他欄位（不含敏感欄位）
    // 可加入 JOIN 計算欄位，例如：
    public int VersionCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**規範**：Domain 不繼承 Entity，是純屬性類別。不得含 PasswordHash、BotTokenEncrypted 等敏感欄位。

---

## 步驟四：建立 Request DTO 與 Response DTO

`DTOs/Requests/Personas/PersonaRequests.cs`
```csharp
namespace ReactL.api.DTOs.Requests.Personas;

public class CreatePersonaRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? SystemPrompt { get; set; }
}

public class UpdatePersonaRequest
{
    [MaxLength(100)]
    public string? Name { get; set; }
}
```

`DTOs/Responses/Personas/PersonaResponses.cs`
```csharp
namespace ReactL.api.DTOs.Responses.Personas;

// 列表用（欄位精簡）
public class PersonaListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

// 詳情用（欄位完整）
public class PersonaDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

---

## 步驟五：建立 Service 介面與實作

`Services/Personas/IPersonaService.cs`
```csharp
namespace ReactL.api.Services.Personas;

public interface IPersonaService
{
    Task<List<PersonaDomain>> GetListAsync(Guid userId);
    Task<PersonaDomain> GetByIdAsync(Guid id, Guid userId);
    Task<PersonaDomain> CreateAsync(CreatePersonaRequest request, Guid userId);
    Task<PersonaDomain> UpdateAsync(Guid id, UpdatePersonaRequest request, Guid userId);
    Task DeleteAsync(Guid id, Guid userId);
}
```

`Services/Personas/PersonaService.cs`
```csharp
public class PersonaService : IPersonaService
{
    private readonly AppDbContext _db;

    public PersonaService(AppDbContext db) => _db = db;

    public async Task<List<PersonaDomain>> GetListAsync(Guid userId)
    {
        return await _db.Personas
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new PersonaDomain
            {
                Id = p.Id,
                Name = p.Name,
                UpdatedAt = p.UpdatedAt
                // map 所有需要的欄位
            })
            .ToListAsync();
    }

    // 其他方法...
}
```

**規範**：Service 回傳 Domain，直接注入 `AppDbContext`，使用 `Select` 或手動投影完成 Entity → Domain 轉換。

---

## 步驟六：建立 Controller

`Controllers/Admin/Personas/PersonasController.cs`
```csharp
[ApiController]
[Route("api/v1/personas")]
[Authorize]
public class PersonasController : ControllerBase
{
    private readonly IPersonaService _service;

    public PersonasController(IPersonaService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var userId = User.GetUserId();
        var domains = await _service.GetListAsync(userId);
        var result = domains.Select(d => new PersonaListItem
        {
            Id = d.Id,
            Name = d.Name,
            UpdatedAt = d.UpdatedAt
        }).ToList();
        return Ok(ApiResponse<List<PersonaListItem>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = User.GetUserId();
        var domain = await _service.GetByIdAsync(id, userId);
        return Ok(ApiResponse<PersonaDetailResponse>.Ok(ToDetailResponse(domain)));
    }

    // 抽出私有 mapping 方法，多個 action 共用
    private static PersonaDetailResponse ToDetailResponse(PersonaDomain d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        SystemPrompt = d.SystemPrompt,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };
}
```

**規範**：Controller 不寫商業邏輯，只做 Domain → Response DTO 轉換。重複 mapping 抽成 `private static ToXxxResponse` 方法。

---

## 步驟七：註冊 DI

`Program.cs`
```csharp
builder.Services.AddScoped<IPersonaService, PersonaService>();
```

---

## 步驟八：執行 Migration

```bash
dotnet ef migrations add AddPersona --project ReactL.api
dotnet ef database update --project ReactL.api
```

---

## 步驟摘要

```
Entity（Models/）
    ↓
Domain（Domain/）     ← Service 回傳型別，不含敏感欄位
    ↓
DTOs/Requests/        ← 前端輸入驗證
DTOs/Responses/       ← API 回傳合約
    ↓
Service（回傳 Domain）
    ↓
Controller（Domain → Response DTO）
    ↓
Program.cs DI 註冊
    ↓
Migration
```