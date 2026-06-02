using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Exceptions;
using System.Diagnostics;
using System.Net.Mime;
using System.Text.Json;

namespace ReactL.api.Middleware
{
    /// <summary>
    /// 全域例外處理中介層，攔截所有未處理例外並統一回傳 RFC 7807 ProblemDetails 格式
    /// 必須在 Program.cs 中排在所有 Middleware 最前面（app.UseMiddleware 第一個呼叫）
    /// 讓所有請求都有例外保護，避免內部錯誤細節洩漏給前端
    /// </summary>
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // traceId 用於關聯 Log 與前端錯誤回報，方便追蹤問題
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            var (statusCode, title, errorCode, details, shouldLog, logLevel) =
                ClassifyException(exception);

            // 依例外類型決定 Log 等級與是否記錄
            if (shouldLog)
            {
                if (logLevel == LogLevel.Error)
                    _logger.LogError(exception, "未預期例外 [{TraceId}] {Path}", traceId, context.Request.Path);
                else
                    _logger.LogWarning(exception, "業務例外 [{TraceId}] {Path} - {Message}", traceId, context.Request.Path, exception.Message);
            }

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message,
                Instance = context.Request.Path
            };

            // 加入自訂欄位（ProblemDetails 允許擴充）
            problemDetails.Extensions["errorCode"] = errorCode;
            problemDetails.Extensions["traceId"] = traceId;

            // ValidationException 附帶欄位錯誤清單
            if (details != null)
                problemDetails.Extensions["errors"] = details;

            context.Response.ContentType = MediaTypeNames.Application.Json;
            context.Response.StatusCode = statusCode;

            var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }

        /// <summary>
        /// 依例外類型判斷 HTTP 狀態碼、標題、是否記錄 Log
        /// 回傳 (statusCode, title, errorCode, details, shouldLog, logLevel)
        /// </summary>
        private static (int, string, string, object?, bool, LogLevel) ClassifyException(Exception ex)
        {
            // 自訂業務例外：依各子類別的 StatusCode 處理
            if (ex is AppException appEx)
            {
                // ForbiddenException 記錄 Warning（可能是未授權存取，值得追蹤）
                bool shouldLog = ex is ForbiddenException;
                return (appEx.StatusCode, appEx.Message, appEx.ErrorCode,
                        appEx.Details, shouldLog, LogLevel.Warning);
            }

            // EF Core 資料庫更新例外：分析 InnerException 判斷具體原因
            if (ex is DbUpdateConcurrencyException)
                return (409, "資料已被其他使用者修改，請重新載入後再試", "CONCURRENCY_CONFLICT",
                        null, true, LogLevel.Warning);

            if (ex is DbUpdateException dbEx)
            {
                var innerMsg = dbEx.InnerException?.Message ?? string.Empty;

                // UNIQUE 約束違反：例如 Email 重複、PersonaVersion 版本號重複
                if (innerMsg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                    return (409, "資料已存在，請確認輸入內容", "DUPLICATE_ENTRY",
                            null, true, LogLevel.Warning);

                // FOREIGN KEY 約束違反：例如 PersonaId 不存在
                if (innerMsg.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase))
                    return (400, "關聯資料不存在，請確認關聯 ID 是否正確", "FOREIGN_KEY_VIOLATION",
                            null, true, LogLevel.Warning);

                // 其他資料庫錯誤
                return (500, "資料庫操作失敗", "DB_ERROR", null, true, LogLevel.Error);
            }

            // 用戶端中斷請求（例如 SSE 串流時使用者切頁）
            // OperationCanceledException 必須在 TaskCanceledException 之前，因為後者是前者的子類別
            if (ex is OperationCanceledException)
                return (499, "請求已取消", "REQUEST_CANCELLED", null, false, LogLevel.Information);

            // HttpRequestException：呼叫外部 AI API 失敗
            if (ex is HttpRequestException)
                return (502, "上游 AI 服務呼叫失敗", "UPSTREAM_ERROR", null, true, LogLevel.Error);

            // TaskCanceledException：呼叫外部 AI API 超時
            if (ex is TaskCanceledException)
                return (504, "上游 AI 服務回應逾時", "UPSTREAM_TIMEOUT", null, true, LogLevel.Error);

            // JSON 格式錯誤（前端傳入非法 JSON）
            if (ex is System.Text.Json.JsonException)
                return (400, "請求格式錯誤，請確認 JSON 格式", "INVALID_JSON", null, false, LogLevel.Information);

            // 其他未預期例外：一律 500，Log Error 含完整 StackTrace
            return (500, "伺服器內部錯誤，請稍後再試", "INTERNAL_ERROR", null, true, LogLevel.Error);
        }
    }
}
