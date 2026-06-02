using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.DTOs.Conversations;
using ReactL.api.Models.Conversations;

namespace ReactL.api.Services.Conversations
{
    public class ConversationService : IConversationService
    {
        private readonly AppDbContext _db;
        private readonly AiSettings _aiSettings;

        public ConversationService(AppDbContext db, IOptions<AiSettings> aiOptions)
        {
            _db = db;
            _aiSettings = aiOptions.Value;
        }

        public async Task<List<ConversationListItem>> GetListAsync(Guid userId)
        {
            return await _db.Conversations
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.UpdatedAt)
                .Select(c => new ConversationListItem
                {
                    Id = c.Id,
                    Title = c.Title,
                    ModelType = c.ModelType,
                    IsPinned = c.IsPinned,
                    IsPublic = c.IsPublic,
                    ShareSlug = c.ShareSlug,
                    PersonaId = c.PersonaId,
                    PersonaName = c.Persona != null ? c.Persona.Name : null,
                    MessageCount = c.Messages.Count,
                    LastMessagePreview = c.Messages
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => m.Content.Length > 100 ? m.Content.Substring(0, 100) : m.Content)
                        .FirstOrDefault(),
                    LastMessageRole = c.Messages
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => m.Role)
                        .FirstOrDefault(),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<ConversationDetailResponse> GetByIdAsync(Guid id, Guid userId)
        {
            var c = await _db.Conversations
                .AsNoTracking()
                .Include(c => c.Persona)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .Where(c => c.Id == id && c.UserId == userId)
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("Conversation", id);

            return ToDetailResponse(c);
        }

        public async Task<ConversationDetailResponse> GetBySlugAsync(string slug)
        {
            var c = await _db.Conversations
                .AsNoTracking()
                .Include(c => c.Persona)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .Where(c => c.ShareSlug == slug && c.IsPublic)
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("Conversation", slug);

            return ToDetailResponse(c);
        }

        public async Task<ConversationDetailResponse> CreateAsync(Guid userId, CreateConversationRequest request)
        {
            ValidateModelType(request.ModelType);
            var conv = new Conversation
            {
                UserId = userId,
                Title = request.Title,
                ModelType = request.ModelType,
                PersonaId = request.PersonaId
            };

            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync();
            return await GetByIdAsync(conv.Id, userId);
        }

        public async Task<ConversationDetailResponse> UpdateAsync(Guid id, Guid userId, UpdateConversationRequest request)
        {
            var conv = await GetOwnedAsync(id, userId);

            if (request.Title != null)
                conv.Title = request.Title;

            if (request.ModelType != null)
            {
                ValidateModelType(request.ModelType);
                conv.ModelType = request.ModelType;
            }

            if (request.IsPinned.HasValue)
                conv.IsPinned = request.IsPinned.Value;

            if (request.IsPublic.HasValue)
            {
                conv.IsPublic = request.IsPublic.Value;
                // 開啟分享時若尚無 Slug 則產生，關閉時清除
                if (request.IsPublic.Value && string.IsNullOrEmpty(conv.ShareSlug))
                    conv.ShareSlug = GenerateSlug();
                else if (!request.IsPublic.Value)
                    conv.ShareSlug = null;
            }

            // UpdatePersona flag 為 true 才更新，允許傳 null 表示清除角色
            if (request.UpdatePersona)
                conv.PersonaId = request.PersonaId;

            await _db.SaveChangesAsync();
            return await GetByIdAsync(id, userId);
        }

        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var conv = await GetOwnedAsync(id, userId);
            conv.IsDeleted = true;
            conv.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<MessageResponse> AddMessageAsync(Guid conversationId, Guid userId, AddMessageRequest request)
        {
            // 確認對話存在且屬於此使用者
            await GetOwnedAsync(conversationId, userId);

            var message = new Message
            {
                ConversationId = conversationId,
                Role = request.Role,
                Content = request.Content,
                TokensIn = request.TokensIn,
                TokensOut = request.TokensOut
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            return new MessageResponse
            {
                Id = message.Id,
                Role = message.Role,
                Content = message.Content,
                TokensIn = message.TokensIn,
                TokensOut = message.TokensOut,
                CreatedAt = message.CreatedAt
            };
        }

        public async Task DeleteLastAssistantMessageAsync(Guid conversationId, Guid userId)
        {
            // 確認對話存在且屬於此使用者
            await GetOwnedAsync(conversationId, userId);

            // 取最後一筆 assistant 訊息；若不存在則不報錯（冪等）
            var lastAssistant = await _db.Messages
                .Where(m => m.ConversationId == conversationId && m.Role == "assistant")
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastAssistant == null) return;

            // 同時刪除緊接在前的 user 訊息，避免 Regenerate 重送時 AI 看到重複問題
            var precedingUser = await _db.Messages
                .Where(m => m.ConversationId == conversationId
                         && m.Role == "user"
                         && m.CreatedAt <= lastAssistant.CreatedAt)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            _db.Messages.Remove(lastAssistant);
            if (precedingUser != null) _db.Messages.Remove(precedingUser);
            await _db.SaveChangesAsync();
        }

        // ── 私有輔助方法 ──────────────────────────────────────────────────────

        private async Task<Conversation> GetOwnedAsync(Guid id, Guid userId)
        {
            return await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId)
                ?? throw new NotFoundException("Conversation", id);
        }

        private static ConversationDetailResponse ToDetailResponse(Conversation c) =>
            new()
            {
                Id = c.Id,
                Title = c.Title,
                ModelType = c.ModelType,
                IsPinned = c.IsPinned,
                IsPublic = c.IsPublic,
                ShareSlug = c.ShareSlug,
                PersonaId = c.PersonaId,
                PersonaName = c.Persona?.Name,
                Messages = c.Messages.Select(m => new MessageResponse
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    TokensIn = m.TokensIn,
                    TokensOut = m.TokensOut,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            };

        /// <summary>產生 5 碼 URL-safe 分享短碼</summary>
        private static string GenerateSlug()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Range(0, 8)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
        }

        /// <summary>驗證 modelType 格式與提供商/模型是否存在，不合規直接 422</summary>
        private void ValidateModelType(string modelType)
        {
            var idx = modelType.IndexOf(':');
            if (idx <= 0 || idx >= modelType.Length - 1)
                throw new ValidationException("modelType",
                    "格式錯誤，請使用 'providerId:modelId'，例如 'groq:llama-3.3-70b-versatile'");

            var providerId = modelType[..idx];
            var modelId = modelType[(idx + 1)..];

            var provider = _aiSettings.Providers.FirstOrDefault(p => p.Id == providerId);
            if (provider == null)
                throw new ValidationException("modelType",
                    $"提供商 '{providerId}' 不存在，可用清單請見 GET /api/v1/ai/providers");

            if (!provider.Models.Any(m => m.Id == modelId))
                throw new ValidationException("modelType",
                    $"模型 '{modelId}' 在提供商 '{providerId}' 下不存在，可用清單請見 GET /api/v1/ai/providers");
        }
    }
}
