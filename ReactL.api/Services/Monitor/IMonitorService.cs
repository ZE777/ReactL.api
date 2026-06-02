using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Monitor;

namespace ReactL.api.Services.Monitor
{
    public interface IMonitorService
    {
        /// <summary>取得分頁的外部訊息列表（Monitor 頁）</summary>
        Task<PagedResponse<ExternalMessageListItem>> GetExternalMessagesAsync(
            Guid userId, MonitorQueryParams query);

        /// <summary>取得 Token 用量統計總覽</summary>
        Task<StatsSummary> GetTokenStatsAsync(Guid userId, StatsQueryParams query);
    }
}
