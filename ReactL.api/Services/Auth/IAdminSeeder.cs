namespace ReactL.api.Services.Auth
{
    /// <summary>啟動時種子 Admin 帳號（若尚無任何 Admin）</summary>
    public interface IAdminSeeder
    {
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}
