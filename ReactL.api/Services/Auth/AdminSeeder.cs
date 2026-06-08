using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Constants;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.Models.Ai;
using ReactL.api.Models.Auth;

namespace ReactL.api.Services.Auth
{
    /// <summary>
    /// 啟動時自動種子一支 Admin 帳號（僅在資料庫尚無任何 Role=Admin 時）。
    /// 帳號以 MustChangePassword=true 建立，首次登入後須強制改密。
    /// </summary>
    public class AdminSeeder : IAdminSeeder
    {
        private readonly AppDbContext _db;
        private readonly AdminSeedSettings _settings;
        private readonly ILogger<AdminSeeder> _logger;

        public AdminSeeder(AppDbContext db, IOptions<AdminSeedSettings> settings, ILogger<AdminSeeder> logger)
        {
            _db = db;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.Enabled) return;

            // 已有任一 Admin → 不重複種子
            var anyAdmin = await _db.Users.AnyAsync(u => u.Role == "Admin", cancellationToken);
            if (anyAdmin) return;

            var email = _settings.Email.Trim().ToLower();

            // Email 已被佔用（例如先前以一般使用者註冊）→ 不覆寫，僅警告
            var emailTaken = await _db.Users.AnyAsync(u => u.Email == email, cancellationToken);
            if (emailTaken)
            {
                _logger.LogWarning("種子 Admin 略過：Email {Email} 已存在，請手動將該帳號 Role 改為 Admin", email);
                return;
            }

            var admin = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(_settings.DefaultPassword),
                DisplayName = _settings.DisplayName,
                Role = "Admin",
                IsActive = true,
                MustChangePassword = true,
            };
            _db.Users.Add(admin);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "已建立種子 Admin 帳號 {Email}（預設密碼來自設定，首次登入後將強制變更）", email);

            // 以系統預設 Key 自動綁定 Admin 的 AI 金鑰，避免首登被「尚未設定金鑰」鎖在 AI 金鑰頁
            await GrantSystemKeysToUserAsync(admin.Id, cancellationToken);
        }

        /// <summary>
        /// 以系統預設 Key 為來源，複製一份綁定到指定使用者（IsSystem=false）。
        /// 直接重用系統 Key 的 AES 密文（同一把金鑰），無需重新加密；冪等：同供應商已存在則略過。
        /// 須於系統 Key 種子（SeedSystemKeysAsync）之後執行——Program.cs 的啟動順序已保證此前置條件。
        /// </summary>
        private async Task GrantSystemKeysToUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            var systemKeys = await _db.AiKeys.AsNoTracking()
                .Where(k => k.UserId == SystemUser.Id && k.IsSystem && k.IsActive)
                .ToListAsync(cancellationToken);

            if (systemKeys.Count == 0)
            {
                _logger.LogWarning("Admin AI 金鑰自動綁定略過：目前尚無任何系統預設 Key（請確認 AiSettings:ProviderKeys 已設定）");
                return;
            }

            var grantedCount = 0;
            foreach (var sk in systemKeys)
            {
                var exists = await _db.AiKeys
                    .AnyAsync(k => k.UserId == userId && k.ProviderId == sk.ProviderId, cancellationToken);
                if (exists) continue;

                _db.AiKeys.Add(new AiKey
                {
                    UserId = userId,
                    ProviderId = sk.ProviderId,
                    EncryptedKey = sk.EncryptedKey,
                    KeyLastFour = sk.KeyLastFour,
                    IsActive = true,
                    IsSystem = false,
                });
                grantedCount++;
            }

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("已為 Admin {UserId} 以系統預設 Key 自動綁定 {Count} 把 AI 金鑰", userId, grantedCount);
        }
    }
}
