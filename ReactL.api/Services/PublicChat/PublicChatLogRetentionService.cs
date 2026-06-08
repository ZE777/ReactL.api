using Microsoft.Extensions.Options;
using ReactL.api.Common.Settings;

namespace ReactL.api.Services.PublicChat
{
    /// <summary>
    /// 前台聊天記錄保留清除背景服務。
    /// 每 24 小時清除一次逾 PublicChatSettings.LogRetentionDays（預設 30）天的 PublicChatLogs。
    /// 啟動後延遲 1 分鐘再首次執行，避免與啟動種子作業搶資源。
    /// </summary>
    public class PublicChatLogRetentionService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PublicChatSettings _settings;
        private readonly ILogger<PublicChatLogRetentionService> _logger;

        public PublicChatLogRetentionService(
            IServiceScopeFactory scopeFactory,
            IOptions<PublicChatSettings> settings,
            ILogger<PublicChatLogRetentionService> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 0 = 永久保留，直接結束此服務
            if (_settings.LogRetentionDays <= 0)
            {
                _logger.LogInformation("前台聊天記錄保留天數為 0（永久保留），略過清除背景服務");
                return;
            }

            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var logService = scope.ServiceProvider.GetRequiredService<IPublicChatLogService>();
                    var deleted = await logService.PurgeExpiredAsync(_settings.LogRetentionDays, stoppingToken);
                    if (deleted > 0)
                        _logger.LogInformation("已清除 {Count} 筆逾 {Days} 天的前台聊天記錄", deleted, _settings.LogRetentionDays);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    // 清除失敗不影響服務存活，下一輪再試
                    _logger.LogWarning(ex, "清除逾期前台聊天記錄失敗，將於下一輪重試");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}