using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.Domain.Conversations;
using ReactL.api.DTOs.Requests.Conversations;
using ReactL.api.Models.Conversations;

namespace ReactL.api.Services.Conversations
{
    /// <summary>對話服務實作</summary>
    public class ConversationService : IConversationService
    {
        private readonly AppDbContext _db;
        private readonly AiSettings _aiSettings;

        public ConversationService(AppDbContext db, IOptions<AiSettings> aiOptions)
        {
            _db = db;
            _aiSettings = aiOptions.Value;
        }

        /// <summary>取得使用者的對話清單（釘選優先，再依更新時間排序）</summary>
        public async Task<List<ConversationDomain>> GetListAsync(Guid userId)
        {
            return await _db.Conversations
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.UpdatedAt)
                .Select(c => new ConversationDomain
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    Title = c.Title,
                    ModelType = c.ModelType,
                    IsPinned = c.IsPinned,
                    IsPublic = c.IsPublic,
                    IsDeleted = c.IsDeleted,
                    ShareSlug = c.ShareSlug,
                    PersonaId = c.PersonaId,
                    PersonaName = c.Persona != null ? c.Persona.Name : null,
                    PersonaEmoji = c.Persona != null ? c.Persona.Emoji : null,
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

        /// <summary>取得對話詳情（含完整訊息列表）</summary>
        public async Task<ConversationDomain> GetByIdAsync(Guid id, Guid userId)
        {
            var c = await _db.Conversations
                .AsNoTracking()
                .Include(c => c.Persona)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .Where(c => c.Id == id && c.UserId == userId)
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("Conversation", id);

            return ToConversationDomain(c);
        }

        /// <summary>
        /// 依 ShareSlug 取得公開對話（不需登入）
        /// IgnoreQueryFilters：分享頁需要顯示已刪除對話的唯讀存檔，繞過 Global Query Filter
        /// </summary>
        public async Task<ConversationDomain> GetBySlugAsync(string slug)
        {
            var c = await _db.Conversations
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(c => c.Persona)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .Where(c => c.ShareSlug == slug && c.IsPublic)
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("Conversation", slug);

            return ToConversationDomain(c);
        }

        /// <summary>建立新對話</summary>
        public async Task<ConversationDomain> CreateAsync(Guid userId, CreateConversationRequest request)
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

        /// <summary>更新對話設定（標題、釘選、公開分享、Persona）</summary>
        public async Task<ConversationDomain> UpdateAsync(Guid id, Guid userId, UpdateConversationRequest request)
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

        /// <summary>軟刪除對話</summary>
        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var conv = await GetOwnedAsync(id, userId);
            conv.IsDeleted = true;
            conv.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        /// <summary>新增單筆訊息</summary>
        public async Task<MessageDomain> AddMessageAsync(Guid conversationId, Guid userId, AddMessageRequest request)
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

            return new MessageDomain
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                Role = message.Role,
                Content = message.Content,
                TokensIn = message.TokensIn,
                TokensOut = message.TokensOut,
                CreatedAt = message.CreatedAt
            };
        }

        /// <summary>刪除最後一筆 assistant 訊息（Regenerate 前清除舊回應，同時清除配對的 user 訊息）</summary>
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

        /// <summary>取得使用者有所有權的 Conversation（已追蹤，可直接修改）</summary>
        private async Task<Conversation> GetOwnedAsync(Guid id, Guid userId)
        {
            return await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId)
                ?? throw new NotFoundException("Conversation", id);
        }

        /// <summary>將 Conversation Entity 投影為業務 Domain 物件</summary>
        private static ConversationDomain ToConversationDomain(Conversation c) =>
            new()
            {
                Id = c.Id,
                UserId = c.UserId,
                Title = c.Title,
                ModelType = c.ModelType,
                IsPinned = c.IsPinned,
                IsPublic = c.IsPublic,
                IsDeleted = c.IsDeleted,
                ShareSlug = c.ShareSlug,
                PersonaId = c.PersonaId,
                PersonaName = c.Persona?.Name,
                PersonaEmoji = c.Persona?.Emoji,
                MessageCount = c.Messages.Count,
                Messages = c.Messages.Select(m => new MessageDomain
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    Role = m.Role,
                    Content = m.Content,
                    TokensIn = m.TokensIn,
                    TokensOut = m.TokensOut,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            };

        /// <summary>產生 8 碼 URL-safe 分享短碼</summary>
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
