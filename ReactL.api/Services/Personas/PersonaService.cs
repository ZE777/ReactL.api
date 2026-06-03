using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Exceptions;
using ReactL.api.Data;
using ReactL.api.Domain.Personas;
using ReactL.api.DTOs.Requests.Personas;
using ReactL.api.DTOs.Responses.Personas;
using ReactL.api.Models.Personas;
using ReactL.api.Services.Ai;

namespace ReactL.api.Services.Personas
{
    /// <summary>Persona 服務實作</summary>
    public class PersonaService : IPersonaService
    {
        private readonly AppDbContext _db;
        private readonly IAiService _ai;
        private readonly ILogger<PersonaService> _logger;

        public PersonaService(AppDbContext db, IAiService ai, ILogger<PersonaService> logger)
        {
            _db = db;
            _ai = ai;
            _logger = logger;
        }

        /// <summary>取得開放前台顯示的 Persona 清單（isBuiltin=true）</summary>
        public async Task<List<PersonaDomain>> GetPublicPersonasAsync()
        {
            return await _db.Personas
                .AsNoTracking()
                .Where(p => p.IsBuiltin)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new PersonaDomain
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    Name = p.Name,
                    Emoji = p.Emoji,
                    SystemPrompt = p.SystemPrompt,
                    PromptSections = p.PromptSections,
                    CurrentVersion = p.CurrentVersion,
                    IsBuiltin = p.IsBuiltin,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
        }

        /// <summary>回傳系統內建（UserId == null）+ 當前使用者自訂的 Persona</summary>
        public async Task<List<PersonaDomain>> GetListAsync(Guid userId)
        {
            return await _db.Personas
                .AsNoTracking()
                .Where(p => p.UserId == null || p.UserId == userId)
                .OrderBy(p => p.IsBuiltin ? 0 : 1)
                .ThenBy(p => p.CreatedAt)
                .Select(p => new PersonaDomain
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    Name = p.Name,
                    Emoji = p.Emoji,
                    SystemPrompt = p.SystemPrompt,
                    PromptSections = p.PromptSections,
                    CurrentVersion = p.CurrentVersion,
                    IsBuiltin = p.IsBuiltin,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
        }

        /// <summary>取得單筆 Persona 詳情</summary>
        public async Task<PersonaDomain> GetByIdAsync(Guid id, Guid userId)
        {
            var p = await _db.Personas
                .AsNoTracking()
                .Where(p => p.Id == id && (p.UserId == null || p.UserId == userId))
                .Select(p => new PersonaDomain
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    Name = p.Name,
                    Emoji = p.Emoji,
                    SystemPrompt = p.SystemPrompt,
                    PromptSections = p.PromptSections,
                    CurrentVersion = p.CurrentVersion,
                    IsBuiltin = p.IsBuiltin,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return p ?? throw new NotFoundException("Persona", id);
        }

        /// <summary>建立新 Persona（同時建立初始版本快照）</summary>
        public async Task<PersonaDomain> CreateAsync(Guid userId, CreatePersonaRequest request)
        {
            var persona = new Persona
            {
                UserId = userId,
                Name = request.Name,
                Emoji = request.Emoji,
                SystemPrompt = request.SystemPrompt,
                PromptSections = request.PromptSections,
                CurrentVersion = 1,
                IsBuiltin = request.IsBuiltin
            };

            _db.Personas.Add(persona);

            // 建立初始版本快照
            _db.PersonaVersions.Add(new PersonaVersion
            {
                PersonaId = persona.Id,
                Version = 1,
                SystemPrompt = request.SystemPrompt,
                PromptSections = request.PromptSections,
                ChangeNote = "初始版本"
            });

            await _db.SaveChangesAsync();
            return await GetByIdAsync(persona.Id, userId);
        }

        /// <summary>更新 Persona（先快照舊版本，再更新欄位，版本號遞增）</summary>
        public async Task<PersonaDomain> UpdateAsync(Guid id, Guid userId, UpdatePersonaRequest request)
        {
            var persona = await GetOwnedPersonaAsync(id, userId);

            // 先快照舊版本，再更新欄位，版本號遞增
            _db.PersonaVersions.Add(new PersonaVersion
            {
                PersonaId = persona.Id,
                Version = persona.CurrentVersion + 1,
                SystemPrompt = request.SystemPrompt,
                PromptSections = request.PromptSections,
                ChangeNote = request.ChangeNote
            });

            persona.Name = request.Name;
            persona.Emoji = request.Emoji;
            persona.SystemPrompt = request.SystemPrompt;
            persona.PromptSections = request.PromptSections;
            persona.IsBuiltin = request.IsBuiltin;
            persona.CurrentVersion += 1;

            await _db.SaveChangesAsync();
            return await GetByIdAsync(id, userId);
        }

        /// <summary>軟刪除 Persona，系統內建（UserId == null）不可刪除</summary>
        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var persona = await GetOwnedPersonaAsync(id, userId);

            // 只有 UserId == null 的系統預設 Persona 不可刪除，使用者自訂但標為公開的仍可刪除
            if (persona.IsBuiltin && persona.UserId == null)
                throw new ForbiddenException("系統內建 Persona 不可刪除");

            persona.IsDeleted = true;
            persona.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        /// <summary>取得指定 Persona 的版本快照清單（確認使用者有存取權後再查詢）</summary>
        public async Task<List<PersonaVersionDomain>> GetVersionsAsync(Guid personaId, Guid userId)
        {
            // 確認使用者有存取權後再查詢版本
            await GetOwnedPersonaAsync(personaId, userId);

            return await _db.PersonaVersions
                .AsNoTracking()
                .Where(v => v.PersonaId == personaId)
                .OrderByDescending(v => v.Version)
                .Select(v => new PersonaVersionDomain
                {
                    Id = v.Id,
                    PersonaId = v.PersonaId,
                    Version = v.Version,
                    SystemPrompt = v.SystemPrompt,
                    PromptSections = v.PromptSections,
                    ChangeNote = v.ChangeNote,
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync();
        }

        /// <summary>取得單一版本快照詳情</summary>
        public async Task<PersonaVersionDomain> GetVersionDetailAsync(Guid personaId, Guid versionId, Guid userId)
        {
            await GetOwnedPersonaAsync(personaId, userId);

            var version = await _db.PersonaVersions
                .AsNoTracking()
                .Where(v => v.Id == versionId && v.PersonaId == personaId)
                .Select(v => new PersonaVersionDomain
                {
                    Id = v.Id,
                    PersonaId = v.PersonaId,
                    Version = v.Version,
                    SystemPrompt = v.SystemPrompt,
                    PromptSections = v.PromptSections,
                    ChangeNote = v.ChangeNote,
                    CreatedAt = v.CreatedAt
                })
                .FirstOrDefaultAsync();

            return version ?? throw new NotFoundException("PersonaVersion", versionId);
        }

        /// <summary>回滾至指定版本（回滾當作一次新修改，版本號繼續遞增，保留完整歷史）</summary>
        public async Task<PersonaDomain> RollbackAsync(Guid personaId, Guid versionId, Guid userId)
        {
            var persona = await GetOwnedPersonaAsync(personaId, userId);

            var targetVersion = await _db.PersonaVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == versionId && v.PersonaId == personaId)
                ?? throw new NotFoundException("PersonaVersion", versionId);

            // 回滾當作一次新修改，版本號繼續遞增，保留完整歷史
            _db.PersonaVersions.Add(new PersonaVersion
            {
                PersonaId = persona.Id,
                Version = persona.CurrentVersion + 1,
                SystemPrompt = targetVersion.SystemPrompt,
                PromptSections = targetVersion.PromptSections,
                ChangeNote = $"回滾至版本 {targetVersion.Version}"
            });

            persona.SystemPrompt = targetVersion.SystemPrompt;
            persona.PromptSections = targetVersion.PromptSections;
            persona.CurrentVersion += 1;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Persona 版本回滾完成 PersonaId={PersonaId} UserId={UserId} TargetVersion={TargetVersion} NewVersion={NewVersion}",
                personaId, userId, targetVersion.Version, persona.CurrentVersion);
            return await GetByIdAsync(personaId, userId);
        }

        /// <summary>
        /// AI 強化 System Prompt（預覽用，不修改 DB）
        /// 要求 AI 以 JSON 格式分別強化各區塊，方便前端逐一填入對應欄位
        /// </summary>
        public async Task<EnhancePromptResponse> EnhancePromptAsync(EnhancePromptRequest request)
        {
            const string systemPrompt =
                "你是一位 prompt engineering 專家，專門優化 AI 角色的 System Prompt 各區塊。" +
                "請根據使用者提供的各區塊內容進行強化，讓角色定義更清晰、指令更具體、邊界更明確。" +
                "你必須以純 JSON 格式回傳，包含以下六個欄位（若原始區塊為空則保持 null）：" +
                "role（角色定義）、background（背景說明）、task（任務範圍）、" +
                "format（回應格式）、constraints（行為限制）、examples（範例）。" +
                "只輸出純 JSON，不要加任何 markdown 格式、說明文字或前言。";

            var s = request.Sections;
            var sb = new StringBuilder();
            sb.AppendLine("請強化以下各區塊，並以 JSON 格式輸出：");
            if (!string.IsNullOrWhiteSpace(s.Role))        sb.AppendLine($"role（角色定義）：{s.Role}");
            if (!string.IsNullOrWhiteSpace(s.Background))  sb.AppendLine($"background（背景說明）：{s.Background}");
            if (!string.IsNullOrWhiteSpace(s.Task))        sb.AppendLine($"task（任務範圍）：{s.Task}");
            if (!string.IsNullOrWhiteSpace(s.Format))      sb.AppendLine($"format（回應格式）：{s.Format}");
            if (!string.IsNullOrWhiteSpace(s.Constraints)) sb.AppendLine($"constraints（行為限制）：{s.Constraints}");
            if (!string.IsNullOrWhiteSpace(s.Examples))    sb.AppendLine($"examples（範例）：{s.Examples}");
            if (!string.IsNullOrWhiteSpace(request.Instruction))
                sb.AppendLine($"\n強化方向：{request.Instruction}");

            _logger.LogInformation("開始 AI 強化 Prompt");
            var rawJson = await _ai.CompleteAsync(systemPrompt, sb.ToString());

            // 剝除 AI 可能包裹的 markdown code block（```json ... ```）
            var json = rawJson.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```");
                if (firstNewline >= 0 && lastFence > firstNewline)
                    json = json[(firstNewline + 1)..lastFence].Trim();
            }

            try
            {
                // PropertyNameCaseInsensitive 相容 AI 回傳大小寫不固定的 key
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var sections = JsonSerializer.Deserialize<PromptSectionsDto>(json, opts) ?? new PromptSectionsDto();
                return new EnhancePromptResponse { Sections = sections };
            }
            catch
            {
                // JSON 解析失敗時降級：將整段結果填入 role 欄位，其餘保持原值
                _logger.LogWarning("AI 強化 Prompt JSON 解析失敗，降級回傳原始結果 RawJsonPreview={Preview}",
                    rawJson[..Math.Min(200, rawJson.Length)]);
                return new EnhancePromptResponse { Sections = new PromptSectionsDto { Role = rawJson } };
            }
        }

        // ── 私有輔助方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 取得使用者有所有權的 Persona（已追蹤，可直接修改）
        /// 若找不到或無權限則拋出對應例外
        /// </summary>
        private async Task<Persona> GetOwnedPersonaAsync(Guid id, Guid userId)
        {
            var persona = await _db.Personas.FindAsync(id)
                ?? throw new NotFoundException("Persona", id);

            // 系統內建（UserId == null）只能讀，不能寫
            if (persona.UserId != userId)
                throw new ForbiddenException("無權限操作此 Persona");

            return persona;
        }
    }
}
