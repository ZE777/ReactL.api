# SQL Scripts 管理流程

## 何時新增 SQL 腳本

| 情境 | 資料夾 | 說明 |
|------|--------|------|
| 初次建立資料庫結構 | `Init/` | EF Migration 執行後，記錄初始 Schema |
| 寫入初始/預設資料 | `Init/` | Roles、預設 Persona 等 Seed 資料 |
| 結構異動（加欄、改型別） | `Changes/` | 對應 EF Migration 的 SQL 版本 |
| 資料修正腳本 | `Changes/` | 一次性資料更新 |

## 命名規則

```
V{版本號}_{動詞}{目標}.sql
```

- 版本號三碼補零：`V001`、`V002`
- `Init/` 和 `Changes/` 各自從 `V001` 獨立計號
- 範例：
  - `Init/V001_CreateSchema.sql`
  - `Init/V002_SeedRoles.sql`
  - `Changes/V001_AddPersonaEmojiColumn.sql`

## 流程

```
開發 EF Entity
    ↓
dotnet ef migrations add {Name}
    ↓
dotnet ef database update
    ↓
將 Migration 對應的 DDL 複製到 SqlScripts/Init 或 Changes
（作為人工可讀的 SQL 紀錄）
```

## 注意事項

- SQL Script 只做**紀錄用途**，實際 DB 異動由 EF Migration 執行
- 不要直接執行 Scripts 到 Production，以 EF Migration 為主
- 每個 Script 頂部加上說明註解：

```sql
-- V001_CreateSchema.sql
-- 建立時間：2026-05-28
-- 說明：初始資料表建立（Personas, Conversations, Messages）
```
