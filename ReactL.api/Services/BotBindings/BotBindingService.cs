using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Helpers;
using ReactL.api.Data;
using ReactL.api.DTOs.BotBindings;
using ReactL.api.Models.BotBindings;

namespace ReactL.api.Services.BotBindings
{
    public class BotBindingService : IBotBindingService
    {
        private readonly AppDbContext _db;
        private readonly AesEncryptionHelper _aes;

        public BotBindingService(AppDbContext db, AesEncryptionHelper aes)
        {
            _db = db;
            _aes = aes;
        }

        public async Task<List<BotBindingListItem>> GetListAsync(Guid userId)
        {
            return await _db.BotBindings
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BotBindingListItem
                {
                    Id = b.Id,
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

        public async Task<BotBindingDetailResponse> GetByIdAsync(Guid id, Guid userId)
        {
            var b = await _db.BotBindings
                .AsNoTracking()
                .Include(b => b.Persona)
                .Where(b => b.Id == id && b.UserId == userId)
                .Select(b => new BotBindingDetailResponse
                {
                    Id = b.Id,
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

        public async Task<BotBindingDetailResponse> CreateAsync(Guid userId, CreateBotBindingRequest request)
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

        public async Task<BotBindingDetailResponse> UpdateAsync(Guid id, Guid userId, UpdateBotBindingRequest request)
        {
            var binding = await GetOwnedAsync(id, userId);

            binding.BotName = request.BotName;
            binding.ModelType = request.ModelType;
            binding.PersonaId = request.PersonaId;
            binding.IsEnabled = request.IsEnabled;

            await _db.SaveChangesAsync();
            return await GetByIdAsync(id, userId);
        }

        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var binding = await GetOwnedAsync(id, userId);
            binding.IsDeleted = true;
            binding.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<BotBindingDetailResponse> RotateTokenAsync(Guid id, Guid userId, RotateTokenRequest request)
        {
            var binding = await GetOwnedAsync(id, userId);

            binding.BotTokenEncrypted = _aes.Encrypt(request.NewToken);
            binding.TokenLastFour = AesEncryptionHelper.GetLastChars(request.NewToken);

            if (request.NewChannelSecret != null)
                binding.ChannelSecretEncrypted = _aes.Encrypt(request.NewChannelSecret);

            await _db.SaveChangesAsync();
            return await GetByIdAsync(id, userId);
        }

        private async Task<BotBinding> GetOwnedAsync(Guid id, Guid userId)
        {
            return await _db.BotBindings
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId)
                ?? throw new NotFoundException("BotBinding", id);
        }
    }
}
