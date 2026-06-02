# Serilog 設定指南

## 套件安裝

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Console
```

## Program.cs 設定

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: false,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();
```

## 產生的檔案格式

```
logs/
  log-20260528.txt   ← 當日所有 log append 到此，不會覆蓋
  log-20260529.txt
```

## 在各層使用

```csharp
// 注入 ILogger<T>（.NET 標準介面，Serilog 會接管）
private readonly ILogger<PersonaService> _logger;

// 結構化 log（必須用具名佔位符）
_logger.LogInformation("建立 Persona: {@Request}", request);
_logger.LogWarning("Persona {Id} 不存在", id);
_logger.LogError(ex, "呼叫 AI API 失敗，Model: {Model}", model);
```

## 請求日誌中介層

在 `Program.cs` 啟用 Serilog 內建的 Request Logging：

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} => {StatusCode} ({Elapsed:0.0000} ms)";
});
```

這樣每支 API 的進出都會自動記錄，不需在 Controller 手動寫。
