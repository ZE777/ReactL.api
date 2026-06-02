using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReactL.api.Data
{
    /// <summary>
    /// EF Core CLI 專用的 DbContext 工廠，供 dotnet ef migrations / database update 使用
    /// 繞過完整的 host 啟動流程（避免 AesEncryptionHelper、JWT 設定等 Singleton 初始化失敗）
    /// 這個類別不影響執行期間的 DI 注入，只在設計時（CLI）使用
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // 直接讀 appsettings.json + appsettings.Development.json，不走 host
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
