namespace ReactL.api.DTOs.Common
{
    /// <summary>
    /// 分頁查詢的通用回應包裝，包含資料清單與分頁中繼資訊
    /// 供列表型 API 使用（Monitor 對話列表、PersonaVersions 等資料量可能較大的端點）
    /// </summary>
    /// <typeparam name="T">清單項目的型別</typeparam>
    public class PagedResponse<T>
    {
        /// <summary>當前頁的資料清單</summary>
        public List<T> Items { get; init; } = [];

        /// <summary>符合條件的總記錄數（不受分頁影響），供前端計算總頁數</summary>
        public int TotalCount { get; init; }

        /// <summary>當前頁碼（從 1 開始）</summary>
        public int Page { get; init; }

        /// <summary>每頁筆數</summary>
        public int PageSize { get; init; }

        /// <summary>總頁數，由 TotalCount / PageSize 計算</summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <summary>是否有下一頁</summary>
        public bool HasNextPage => Page < TotalPages;

        /// <summary>是否有上一頁</summary>
        public bool HasPreviousPage => Page > 1;
    }
}
