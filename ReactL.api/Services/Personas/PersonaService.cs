using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Exceptions;
using ReactL.api.Data;
using ReactL.api.DTOs.Personas;
using ReactL.api.Models.Personas;
using ReactL.api.Services.Ai;

namespace ReactL.api.Services.Personas
{
    public class PersonaService : IPersonaService
    {
        private readonly AppDbContext _db;
        private readonly IAiService _ai;

        public PersonaService(AppDbContext db, IAiService ai)
        {
            _db = db;
            _ai = ai;
        }

        public async Task<List<PersonaListItem>> GetListAsync(Guid userId)
        {
            // 回傳系統內建（UserId == null）+ 當前使用者自訂的 Persona
            return await _db.Personas
                .AsNoTracking()
                .Where(p => p.UserId == null || p.UserId == userId)
                .OrderBy(p => p.IsBuiltin ? 0 : 1)
                .ThenBy(p => p.CreatedAt)
                .Select(p => new PersonaListItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    Emoji = p.Emoji,
                    CurrentVersion = p.CurrentVersion,
                    IsBuiltin = p.IsBuiltin,
                    UserId = p.UserId,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<PersonaDetailResponse> GetByIdAsync(Guid id, Guid userId)
        {
            var p = await _db.Personas
                .AsNoTracking()
                .Where(p => p.Id == id && (p.UserId == null || p.UserId == userId))
                .Select(p => new PersonaDetailResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Emoji = p.Emoji,
                    SystemPrompt = p.SystemPrompt,
                    PromptSections = p.PromptSections,
                    CurrentVersion = p.CurrentVersion,
                    IsBuiltin = p.IsBuiltin,
                    UserId = p.UserId,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return p ?? throw new NotFoundException("Persona", id);
        }

        public async Task<PersonaDetailResponse> CreateAsync(Guid userId, CreatePersonaRequest request)
        {
            var persona = new Persona
            {
                UserId = userId,
                Name = request.Name,
                Emoji = request.Emoji,
                SystemPrompt = request.SystemPrompt,
                PromptSections = request.PromptSections,
                CurrentVersion = 1
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

        public async Task<PersonaDetailResponse> UpdateAsync(Guid id, Guid userId, UpdatePersonaRequest request)
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
            persona.CurrentVersion += 1;

            await _db.SaveChangesAsync();
            return await GetByIdAsync(id, userId);
        }

        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var persona = await GetOwnedPersonaAsync(id, userId);

            if (persona.IsBuiltin)
                throw new ForbiddenException("系統內建 Persona 不可刪除");

            persona.IsDeleted = true;
            persona.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<List<PersonaVersionItem>> GetVersionsAsync(Guid personaId, Guid userId)
        {
            // 確認使用者有存取權後再查詢版本
            await GetOwnedPersonaAsync(personaId, userId);

            return await _db.PersonaVersions
                .AsNoTracking()
                .Where(v => v.PersonaId == personaId)
                .OrderByDescending(v => v.Version)
                .Select(v => new PersonaVersionItem
                {
                    Id = v.Id,
                    Version = v.Version,
                    SystemPrompt = v.SystemPrompt,
                    ChangeNote = v.ChangeNote,
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<PersonaVersionDetailResponse> GetVersionDetailAsync(Guid personaId, Guid versionId, Guid userId)
        {
            await GetOwnedPersonaAsync(personaId, userId);

            var version = await _db.PersonaVersions
                .AsNoTracking()
                .Where(v => v.Id == versionId && v.PersonaId == personaId)
                .Select(v => new PersonaVersionDetailResponse
                {
                    Id = v.Id,
                    Version = v.Version,
                    SystemPrompt = v.SystemPrompt,
                    PromptSections = v.PromptSections,
                    ChangeNote = v.ChangeNote,
                    CreatedAt = v.CreatedAt
                })
                .FirstOrDefaultAsync();

            return version ?? throw new NotFoundException("PersonaVersion", versionId);
        }

        public async Task<PersonaDetailResponse> RollbackAsync(Guid personaId, Guid versionId, Guid userId)
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
            return await GetByIdAsync(personaId, userId);
        }

        public async Task<EnhancePromptResponse> EnhancePromptAsync(EnhancePromptRequest request)
        {
            // 要求 AI 以 JSON 格式分別強化各區塊，方便前端逐一填入對應欄位
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
