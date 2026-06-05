using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ReactL.api.Common.Helpers;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.Middleware;
using ReactL.api.Services.Ai;
using ReactL.api.Services.Auth;
using ReactL.api.Services.BotBindings;
using ReactL.api.Services.Conversations;
using ReactL.api.Services.Monitor;
using ReactL.api.Services.Personas;
using ReactL.api.Services.PromptTemplates;
using ReactL.api.Services.Users;
using ReactL.api.Services.Webhooks;
using Serilog;
using System.Text;

namespace ReactL.api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Serilog 在 WebApplication 建立之前初始化，確保啟動期間的錯誤也能被記錄
            // RollingInterval.Day：每日建立新檔，同一天的 log 持續 append，不覆蓋
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File(
                    path: "logs/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,   // 最多保留 30 天的 log 檔
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            try
            {
                Log.Information("啟動 ReactL API");

                var builder = WebApplication.CreateBuilder(args);

                // 將 .NET 內建的 ILogger 接管給 Serilog，統一由 Serilog 輸出
                builder.Host.UseSerilog();

                // ── 設定類別注入 ──────────────────────────────────────────────────────
                // 使用強型別設定類別，避免在程式碼中直接讀取字串 key（易打錯、難重構）
                // 各設定對應 appsettings.json 中的同名 section
                // 敏感值（ApiKey、SecretKey、Token）應透過 User Secrets 或環境變數覆蓋，不寫入版控
                builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
                builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
                builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("AiSettings"));
                builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("CorsSettings"));
                builder.Services.Configure<EncryptionSettings>(builder.Configuration.GetSection("EncryptionSettings"));

                // ── Controllers & Swagger ─────────────────────────────────────────────
                // 使用 AddControllers 而非 AddControllersWithViews，純 API 不需要 Razor View 引擎
                builder.Services.AddControllers();
                builder.Services.AddEndpointsApiExplorer();

                // Swagger：自動掃描 Controller / Action 產生 RESTful API 文件與測試介面
                builder.Services.AddSwaggerGen(options =>
                {
                    // API 基本資訊，顯示在 Swagger UI 頁面頂部
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "ReactL Prompt Studio API",
                        Version = "v1",
                        Description = "AI Prompt Studio 後端 API，支援角色、模板、Bot 管理與對話功能"
                    });

                    // 讀取 csproj 產生的 XML 文件，讓 Controller 的 <summary> 顯示在 Swagger UI
                    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                    options.IncludeXmlComments(xmlPath);

                    // 在 Swagger UI 加入 JWT Bearer Token 輸入框
                    // 讓開發者可以直接在 Swagger UI 測試需要登入的端點（貼入 Token 後自動帶入 Authorization Header）
                    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.Http,
                        Scheme = JwtBearerDefaults.AuthenticationScheme,
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description = "請輸入 JWT Token（不需要加 Bearer 前綴，Swagger 會自動加上）"
                    });

                    // 全域套用 JWT 安全需求，所有標記 [Authorize] 的端點都會顯示鎖頭圖示
                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = JwtBearerDefaults.AuthenticationScheme
                                }
                            },
                            []
                        }
                    });
                });

                // ── CORS（跨域資源共用）────────────────────────────────────────────────
                // 允許前端（Next.js / Vite）呼叫本 API
                // AllowedOrigins 從設定檔讀取，方便依環境切換（dev: localhost，prod: IIS 站台 URL）
                var allowedOrigins = builder.Configuration
                    .GetSection("CorsSettings:AllowedOrigins")
                    .Get<string[]>() ?? [];

                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowFrontend", policy =>
                        policy
                            .WithOrigins(allowedOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod());
                });

                // ── 資料庫 DbContext ──────────────────────────────────────────────────
                builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

                // ── AES 加密工具（Singleton：Key/IV 在啟動時載入一次，執行期間不變）────
                builder.Services.AddSingleton<AesEncryptionHelper>();

                // ── 健康檢查 ──────────────────────────────────────────────────────────
                // 供 IIS 或監控系統呼叫 GET /health 確認服務與 DB 連線是否正常
                builder.Services.AddHealthChecks()
                    .AddDbContextCheck<AppDbContext>("database");

                // ── JWT 認證 ─────────────────────────────────────────────────────────
                // SecretKey 透過 User Secrets / 環境變數注入，此處從設定讀取已合併的值
                var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = jwtSettings.Issuer,
                            ValidateAudience = true,
                            ValidAudience = jwtSettings.Audience,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                            // 允許 1 分鐘的時鐘偏差，容忍伺服器間的時間差
                            ClockSkew = TimeSpan.FromMinutes(1)
                        };
                    });

                // ── Services DI 註冊 ──────────────────────────────────────────────────
                builder.Services.AddScoped<IAuthService, AuthService>();
                builder.Services.AddScoped<IUserService, UserService>();
                builder.Services.AddScoped<IPersonaService, PersonaService>();
                builder.Services.AddScoped<IPromptTemplateService, PromptTemplateService>();
                builder.Services.AddScoped<IBotBindingService, BotBindingService>();
                builder.Services.AddScoped<IConversationService, ConversationService>();
                builder.Services.AddScoped<IAiService, OpenAiService>();
                builder.Services.AddScoped<IAiKeyService, AiKeyService>();
                builder.Services.AddScoped<IMonitorService, MonitorService>();
                builder.Services.AddScoped<ILineWebhookService, LineWebhookService>();
                builder.Services.AddScoped<IDiscordWebhookService, DiscordWebhookService>();
                builder.Services.AddScoped<IDiscordCommandService, DiscordCommandService>();
                builder.Services.AddScoped<ILineCredentialService, LineCredentialService>();

                // ── HttpClient（多 AI 提供商）─────────────────────────────────────────────
                // 使用 IHttpClientFactory 管理 HttpClient 生命週期，避免 Socket 耗盡
                // 依 appsettings.json Providers 清單動態註冊，名稱格式 "ai:{providerId}"
                var aiSettings = builder.Configuration.GetSection("AiSettings").Get<AiSettings>()!;
                foreach (var provider in aiSettings.Providers)
                {
                    var providerKey = aiSettings.ProviderKeys.GetValueOrDefault(provider.Id, string.Empty);
                    var providerId = provider.Id;
                    var baseUrl = provider.BaseUrl;
                    builder.Services.AddHttpClient($"ai:{providerId}", client =>
                    {
                        client.BaseAddress = new Uri(baseUrl);
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", providerKey);
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("ReactL-API/1.0");
                        // 部分 AI Provider 的 CDN 對 HTTP/2 支援不穩，強制 HTTP/1.1 避免 404
                        client.DefaultRequestVersion = System.Net.HttpVersion.Version11;
                        client.DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrLower;
                        client.Timeout = TimeSpan.FromSeconds(aiSettings.TimeoutSeconds);
                    });
                }

                var app = builder.Build();

                // ── 開發環境才啟用 Swagger UI ─────────────────────────────────────────
                // Production 不對外暴露 API 文件，避免洩漏端點資訊
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                app.UseHttpsRedirection();

                // CORS 必須在 UseAuthentication / UseAuthorization 之前，否則 Preflight 請求會被攔截
                app.UseCors("AllowFrontend");

                // 每支 API 的進出都會自動記錄 Method、Path、StatusCode、耗時
                // Controller 不需要再手動寫 HTTP 層的進出 log
                app.UseSerilogRequestLogging(options =>
                {
                    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} => {StatusCode} ({Elapsed:0.0000} ms)";
                });

                // 統一例外處理 Middleware，必須放在所有 Middleware 最前面
                // 確保任何後續 Middleware 或 Controller 拋出的例外都能被捕捉並格式化
                app.UseMiddleware<ExceptionMiddleware>();

                // UseAuthentication 必須在 UseAuthorization 之前
                app.UseAuthentication();
                app.UseAuthorization();

                app.MapHealthChecks("/health");
                app.MapControllers();

                // ── 啟動種子：將 appsettings 的 AI 預設 Key 加密寫入 DB（冪等）──────────
                // 失敗（例如 AiKeys 表尚未建立）只記 Warning，不阻擋 API 啟動
                using (var scope = app.Services.CreateScope())
                {
                    try
                    {
                        var aiKeySeeder = scope.ServiceProvider.GetRequiredService<IAiKeyService>();
                        aiKeySeeder.SeedSystemKeysAsync(CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch (Exception seedEx)
                    {
                        Log.Warning(seedEx, "系統預設 AI Key 種子寫入失敗（可能 AiKeys 表尚未建立），略過");
                    }
                }

                app.Run();
            }
            catch (Exception ex)
            {
                // 啟動失敗（例如 DB 連線失敗、設定錯誤）記錄 Fatal 並終止
                Log.Fatal(ex, "API 啟動失敗");
            }
            finally
            {
                // 確保所有緩衝的 log 在程式結束前都寫入磁碟
                Log.CloseAndFlush();
            }
        }
    }
}