# 新增 API 端點流程

每次新增一個資源的 CRUD，依照以下順序進行。

## 步驟一：建立 Entity

`Models/Persona.cs`
```csharp
public class Persona : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
}
```

## 步驟二：更新 DbContext

`Data/AppDbContext.cs`
```csharp
public DbSet<Persona> Personas => Set<Persona>();
```

## 步驟三：建立 DTO

`DTOs/Persona/CreatePersonaRequest.cs`
```csharp
public class CreatePersonaRequest
{
    public string Name { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
}

public class PersonaResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

## 步驟四：建立 Repository 介面與實作

`Interfaces/IPersonaRepository.cs`
```csharp
public interface IPersonaRepository
{
    Task<List<Persona>> GetAllAsync();
    Task<Persona?> GetByIdAsync(int id);
    Task<Persona> CreateAsync(Persona entity);
    Task UpdateAsync(Persona entity);
    Task DeleteAsync(int id);
}
```

`Repositories/PersonaRepository.cs` — 實作介面，注入 AppDbContext

## 步驟五：建立 Service 介面與實作

`Interfaces/IPersonaService.cs`
```csharp
public interface IPersonaService
{
    Task<List<PersonaResponse>> GetAllAsync();
    Task<PersonaResponse?> GetByIdAsync(int id);
    Task<PersonaResponse> CreateAsync(CreatePersonaRequest request);
    Task UpdateAsync(int id, UpdatePersonaRequest request);
    Task DeleteAsync(int id);
}
```

`Services/PersonaService.cs` — 實作商業邏輯，注入 IPersonaRepository

## 步驟六：建立 Controller

`Controllers/PersonaController.cs`
```csharp
[ApiController]
[Route("api/v1/personas")]
public class PersonaController : ControllerBase
{
    private readonly IPersonaService _service;
    private readonly ILogger<PersonaController> _logger;

    public PersonaController(IPersonaService service, ILogger<PersonaController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<PersonaResponse>>.Ok(result));
    }
}
```

## 步驟七：註冊 DI

`Program.cs`
```csharp
builder.Services.AddScoped<IPersonaRepository, PersonaRepository>();
builder.Services.AddScoped<IPersonaService, PersonaService>();
```

## 步驟八：執行 Migration

```bash
dotnet ef migrations add AddPersona
dotnet ef database update
```
