# API 回應格式規則

## 統一包裝格式

所有 API 回應都使用 `ApiResponse<T>` 包裝，前端統一解析。

```json
// 成功
{
  "success": true,
  "data": { ... },
  "message": null
}

// 失敗
{
  "success": false,
  "data": null,
  "message": "錯誤說明"
}
```

## HTTP 狀態碼規範

| 情境 | 狀態碼 |
|------|--------|
| 查詢成功 | 200 OK |
| 新增成功 | 201 Created |
| 無回傳內容（刪除） | 204 No Content |
| 驗證失敗 / 業務邏輯錯誤 | 400 Bad Request |
| 未登入 | 401 Unauthorized |
| 無權限 | 403 Forbidden |
| 資源不存在 | 404 Not Found |
| 伺服器錯誤 | 500 Internal Server Error |

## Controller 回傳範例

```csharp
// 成功
return Ok(ApiResponse<PersonaResponse>.Ok(result));

// 找不到
return NotFound(ApiResponse<object>.Fail("Persona 不存在"));

// 錯誤（由 Middleware 統一處理，Controller 不需要自己 try-catch）
```

## 錯誤由 Middleware 統一攔截

`Middleware/ExceptionMiddleware.cs` 攔截所有未處理例外，  
統一回傳 500 格式，並寫入 Serilog Error log。  
Controller 和 Service 只需 throw，不需包 try-catch。
