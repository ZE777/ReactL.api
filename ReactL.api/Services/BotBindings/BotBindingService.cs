using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Helpers;
using ReactL.api.Data;
using ReactL.api.Domain.BotBindings;
using ReactL.api.DTOs.Requests.BotBindings;
using ReactL.api.Models.BotBindings;

namespace ReactL.api.Services.BotBindings
{
    /// <summary>Bot 綁定服務實作</summary>
    public class BotBindingService : IBotBindingService
    {
        private readonly AppDbContext _db;
        private readonly AesEncryptionHelper _aes;

        public BotBindingService(AppDbContext db, AesEncryptionHelper aes)
        {
            _db = db;
            _aes = aes;
        }

        /// <summary>取得使用者的 Bot 綁定清單</summary>
        public async Task<List<BotBindingDomain>> GetListAsync(Guid userId)
        {
            return await _db.BotBindings
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BotBindingDomain
                {
                    Id = b.Id,
                    UserId = b.UserId,
                    Platform = b.Platform,
                    BotName = b.BotName,
                    TokenLastFour = b.TokenLastFour,
                    ModelType = b.ModelType,
                    IsEnabled = b.IsEnabled,
                    PersonaId = b.PersonaId,
                    PersonaName = b.Persona != null ? b.Persona.Name : null,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt
                })
                .ToListAsync();
        }

        /// <summary>取得 Bot 綁定詳情</summary>
        public async Task<BotBindingDomain> GetByIdAsync(Guid id, Guid userId)
        {
            var b = await _db.BotBindings
                .AsNoTracking()
                .Include(b => b.Persona)
                .Where(b => b.Id == id && b.UserId == userId)
                .Select(b => new BotBindingDomain
                {
                    Id = b.Id,
                    UserId = b.UserId,
                    Platform = b.Platform,
                    BotName = b.BotName,
                    TokenLastFour = b.TokenLastFour,
                    ModelType = b.ModelType,
                    IsEnabled = b.IsEnabled,
                    PersonaId = b.PersonaId,
                    PersonaName = b.Persona != null ? b.Persona.Name : null,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return b ?? throw new NotFoundException("BotBinding", id);
        }

        /// <summary>建立 Bot 綁定（LINE 平台必須提供 ChannelSecret，Token 由後端加密儲存）</summary>
        public async Task<BotBindingDomain> CreateAsync(Guid userId, CreateBotBindingRequest request)
        {
            // LINE 平台必須提供 ChannelSecret
            if (request.Platform == "line" && string.IsNullOrWhiteSpace(request.ChannelSecret))
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["channelSecret"] = ["LINE 平台必須提供 Channel Secret"]
                });

            var binding = new BotBinding
            {
                UserId = userId,
                Platform = request.Platform,
                BotName = request.BotName,
                BotTokenEncrypted = _aes.Encrypt(request.BotToken),
                ChannelSecretEncrypted = request.ChannelSecret != null
                    ? _aes.Encrypt(request.ChannelSecret)
                    : null,
                // 儲存後 4 碼供列表顯示，避免每筆列表都解密
                TokenLastFour = AesEncryptionHelper.GetLastChars(request.BotToken),
                ModelType = request.ModelType,
                PersonaId = request.PersonaId
            };

            _db.BotBindings.Add(binding);
            await _db.SaveChangesAsync();
            return await GetByIdAsync(binding.Id, userId);
        }

        /// <summary>更新 Bot 設定（名稱、模型、Persona、啟用狀態）</summary>
        public async Task<BotBindingDomain> UpdateAsync(Guid id, Guid userId, UpdateBotBindingRequest request)
        {
            var binding = await GetOwnedAsync(id, userId);

            binding.BotName = request.BotName;
            binding.ModelType = request.ModelType;
            binding.PersonaId = request.PersonaId;
            binding.IsEnabled = request.IsEnabled;

            await _db.SaveChangesAsync();
            return await GetByIdAsync(id, userId);
        }

        /// <summary>軟刪除 Bot 綁定</summary>
        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var binding = await GetOwnedAsync(id, userId);
            binding.IsDeleted = true;
            binding.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        /// <summary>更換 Bot Token（Token 重新 AES 加密後存回 DB）</summary>
        public async Task<BotBindingDomain> RotateTokenAsync(Guid id, Guid userId, RotateTokenRequest request)
        {
            var binding = await GetOwnedAsync(id, userId);

            binding.BotTokenEncrypted = _aes.Encrypt(request.NewToken);
            binding.TokenLastFour = AesEncryptionHelper.GetLastChars(request.NewToken);

            if (request.NewChannelSecret != null)
                binding.ChannelSecretEncrypted = _aes.Encrypt(request.NewChannelSecret);

            await _db.SaveChangesAsync();
            return await GetByIdAsync(id, userId);
        }

        /// <summary>取得使用者有所有權的 BotBinding（已追蹤，可直接修改）</summary>
        private async Task<BotBinding> GetOwnedAsync(Guid id, Guid userId)
        {
            return await _db.BotBindings
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId)
                ?? throw new NotFoundException("BotBinding", id);
        }
    }
}
