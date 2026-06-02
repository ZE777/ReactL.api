using Microsoft.EntityFrameworkCore;
using ReactL.api.Models.Auth;
using ReactL.api.Models.Base;
using ReactL.api.Models.BotBindings;
using ReactL.api.Models.Conversations;
using ReactL.api.Models.External;
using ReactL.api.Models.Personas;
using ReactL.api.Models.PromptTemplates;
using ReactL.api.Models.Stats;

// 注意：Model 檔案的 namespace 需對應資料夾結構
// Models/Auth/ → namespace ReactL.api.Models.Auth
// Models/Personas/ → namespace ReactL.api.Models.Personas
// 以此類推

namespace ReactL.api.Data
{
    /// <summary>
    /// 應用程式資料庫上下文，統一管理所有 Entity 的 EF Core 配置
    /// 負責：自動設定 CreatedAt/UpdatedAt、Global Query Filter（軟刪除）、資料表結構配置
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ── DbSet 定義 ────────────────────────────────────────────────────
        public DbSet<User> Users => Set<User>();
        public DbSet<Persona> Personas => Set<Persona>();
        public DbSet<PersonaVersion> PersonaVersions => Set<PersonaVersion>();
        public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
        public DbSet<BotBinding> BotBindings => Set<BotBinding>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<ExternalMessage> ExternalMessages => Set<ExternalMessage>();
        public DbSet<TokenUsageStat> TokenUsageStats => Set<TokenUsageStat>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Global Query Filter（軟刪除）──────────────────────────────
            // 自動為所有繼承 SoftDeletableEntity 的 Entity 加上 WHERE IsDeleted = 0
            // 若需要查詢已刪除資料，在查詢鏈加上 .IgnoreQueryFilters()
            modelBuilder.Entity<Persona>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<PromptTemplate>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<BotBinding>().HasQueryFilter(b => !b.IsDeleted);
            modelBuilder.Entity<Conversation>().HasQueryFilter(c => !c.IsDeleted);

            // ── Users ─────────────────────────────────────────────────────
            modelBuilder.Entity<User>(e =>
            {
                e.Property(u => u.Email).HasMaxLength(256).IsRequired();
                e.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
                e.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
                e.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("User");
                e.HasIndex(u => u.Email).IsUnique().HasDatabaseName("IX_Users_Email");
            });

            // ── Personas ──────────────────────────────────────────────────
            modelBuilder.Entity<Persona>(e =>
            {
                e.Property(p => p.Name).HasMaxLength(100).IsRequired();
                e.Property(p => p.Emoji).HasMaxLength(10);
                e.Property(p => p.SystemPrompt).HasColumnType("nvarchar(max)").IsRequired();
                e.Property(p => p.PromptSections).HasColumnType("nvarchar(max)");
                e.HasIndex(p => p.UserId).HasDatabaseName("IX_Personas_UserId");
                e.HasIndex(p => p.IsDeleted).HasDatabaseName("IX_Personas_IsDeleted");

                // 刪除 User 時，Persona 的 UserId 設為 NULL（系統內建 Persona 本來就是 NULL，自訂 Persona 設為 NULL 保留記錄）
                e.HasOne(p => p.User)
                 .WithMany(u => u.Personas)
                 .HasForeignKey(p => p.UserId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── PersonaVersions ───────────────────────────────────────────
            modelBuilder.Entity<PersonaVersion>(e =>
            {
                e.Property(pv => pv.SystemPrompt).HasColumnType("nvarchar(max)").IsRequired();
                e.Property(pv => pv.PromptSections).HasColumnType("nvarchar(max)");
                e.Property(pv => pv.ChangeNote).HasMaxLength(500);
                e.HasIndex(pv => pv.PersonaId).HasDatabaseName("IX_PersonaVersions_PersonaId");

                // 版本號在同一 Persona 內不可重複
                e.HasIndex(pv => new { pv.PersonaId, pv.Version })
                 .IsUnique()
                 .HasDatabaseName("UX_PersonaVersions_PersonaId_Version");

                // 刪除 Persona 時 CASCADE DELETE 清除所有版本快照
                e.HasOne(pv => pv.Persona)
                 .WithMany(p => p.Versions)
                 .HasForeignKey(pv => pv.PersonaId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── PromptTemplates ───────────────────────────────────────────
            modelBuilder.Entity<PromptTemplate>(e =>
            {
                e.Property(pt => pt.Title).HasMaxLength(200).IsRequired();
                e.Property(pt => pt.Content).HasColumnType("nvarchar(max)").IsRequired();
                e.Property(pt => pt.Category).HasMaxLength(50).HasDefaultValue("其他");
                e.Property(pt => pt.Tags).HasMaxLength(500);
                e.HasIndex(pt => pt.UserId).HasDatabaseName("IX_PromptTemplates_UserId");
                e.HasIndex(pt => pt.Category).HasDatabaseName("IX_PromptTemplates_Category");
            });

            // ── BotBindings ───────────────────────────────────────────────
            modelBuilder.Entity<BotBinding>(e =>
            {
                e.Property(b => b.Platform).HasMaxLength(20).IsRequired();
                e.Property(b => b.BotName).HasMaxLength(100).IsRequired();
                e.Property(b => b.BotTokenEncrypted).HasMaxLength(700).IsRequired();
                e.Property(b => b.ChannelSecretEncrypted).HasMaxLength(700);
                e.Property(b => b.TokenLastFour).HasMaxLength(4).IsRequired();
                e.Property(b => b.ModelType).HasMaxLength(50).IsRequired();
                e.HasIndex(b => b.UserId).HasDatabaseName("IX_BotBindings_UserId");
                e.HasIndex(b => b.Platform).HasDatabaseName("IX_BotBindings_Platform");

                // 刪除 Persona 時，BotBinding 的 PersonaId 設為 NULL，Bot 繼續存在但不套用角色
                e.HasOne(b => b.Persona)
                 .WithMany(p => p.BotBindings)
                 .HasForeignKey(b => b.PersonaId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Conversations ─────────────────────────────────────────────
            modelBuilder.Entity<Conversation>(e =>
            {
                e.Property(c => c.Title).HasMaxLength(200).HasDefaultValue("新對話");
                e.Property(c => c.ModelType).HasMaxLength(50).IsRequired();
                e.Property(c => c.ShareSlug).HasMaxLength(20);
                e.HasIndex(c => c.UserId).HasDatabaseName("IX_Conversations_UserId");

                // FILTERED UNIQUE INDEX：ShareSlug 在非 NULL 時才套用唯一性約束
                e.HasIndex(c => c.ShareSlug)
                 .IsUnique()
                 .HasFilter("[ShareSlug] IS NOT NULL")
                 .HasDatabaseName("IX_Conversations_ShareSlug");

                // 刪除 Persona 時，Conversation 的 PersonaId 設為 NULL
                e.HasOne(c => c.Persona)
                 .WithMany(p => p.Conversations)
                 .HasForeignKey(c => c.PersonaId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Messages ──────────────────────────────────────────────────
            modelBuilder.Entity<Message>(e =>
            {
                e.Property(m => m.Role).HasMaxLength(20).IsRequired();
                e.Property(m => m.Content).HasColumnType("nvarchar(max)").IsRequired();
                e.HasIndex(m => m.ConversationId).HasDatabaseName("IX_Messages_ConversationId");

                // 刪除 Conversation 時 CASCADE DELETE 清除所有訊息
                e.HasOne(m => m.Conversation)
                 .WithMany(c => c.Messages)
                 .HasForeignKey(m => m.ConversationId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── ExternalMessages ──────────────────────────────────────────
            modelBuilder.Entity<ExternalMessage>(e =>
            {
                e.Property(em => em.Platform).HasMaxLength(20).IsRequired();
                e.Property(em => em.ExternalUserId).HasMaxLength(200).IsRequired();
                e.Property(em => em.ExternalChannelId).HasMaxLength(200);
                e.Property(em => em.Role).HasMaxLength(20).IsRequired();
                e.Property(em => em.Content).HasColumnType("nvarchar(max)").IsRequired();
                e.HasIndex(em => em.BotBindingId).HasDatabaseName("IX_ExternalMessages_BotBindingId");

                // 複合索引：Monitor 頁常用 Platform + 時間範圍篩選
                e.HasIndex(em => new { em.Platform, em.CreatedAt })
                 .HasDatabaseName("IX_ExternalMessages_Platform_CreatedAt");

                e.HasIndex(em => em.ExternalUserId).HasDatabaseName("IX_ExternalMessages_ExternalUserId");
            });

            // ── TokenUsageStats ───────────────────────────────────────────
            modelBuilder.Entity<TokenUsageStat>(e =>
            {
                e.Property(t => t.ModelType).HasMaxLength(50).IsRequired();
                e.Property(t => t.Source).HasMaxLength(20).IsRequired();

                // 唯一索引：同一使用者在同一天同一模型同一來源只有一筆彙總記錄，用於 UPSERT
                e.HasIndex(t => new { t.UserId, t.Date, t.ModelType, t.Source })
                 .IsUnique()
                 .HasDatabaseName("UX_TokenUsageStats_UserId_Date_Model_Source");

                e.HasIndex(t => new { t.UserId, t.Date })
                 .HasDatabaseName("IX_TokenUsageStats_UserId_Date");
            });
        }

        /// <summary>
        /// 覆寫 SaveChangesAsync，在儲存前統一設定時間戳記
        /// 避免各 Service 層手動設定 CreatedAt / UpdatedAt，確保使用 UTC 時間
        /// </summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                // 新增時設定 CreatedAt；修改時不覆寫（保留原始建立時間）
                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedAt = now;
            }

            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                // 新增和修改時都更新 UpdatedAt
                if (entry.State is EntityState.Added or EntityState.Modified)
                    entry.Entity.UpdatedAt = now;
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
