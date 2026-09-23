# Chrome 书签管理器（Chrome Bookmarks Manager）

[English](README.md) | **简体中文**

Chrome Bookmarks Manager 是一款面向 Windows 的桌面书签管理工具，主要用于管理体量很大的 Chrome 书签库。项目采用本地优先（Local-first）设计，重点强调明确的数据安全边界、可复现构建，以及对 Chrome 原生 `Bookmarks` 文件的安全编辑与保存。

> 本项目为第三方个人工具，与 Google Chrome 官方没有隶属关系。

## 当前版本

**稳定版 — V1.2.0**

V1.2.0 是当前稳定的个人使用版本。在完整中英双语界面、Windows 品牌 Icon 与 V1.0 Safe Save 安全机制的基础上，V1.2.0 新增 Chrome 原生 `Bookmarks` 文件直接拖放打开，以及批量书签添加功能。批量添加支持多行网址输入与 UTF-8 TXT 文件导入，可从顶部“编辑”菜单、左侧文件夹树和右侧内容列表进入，并提供无效行拦截与整批单步 Undo / Redo。

当前版本包括：

- 首次启动默认使用简体中文
- 支持简体中文 / English 即时切换，无需重启
- 自动记住上一次选择的界面语言
- 语言设置独立存放，不会写入 Chrome `Bookmarks` 文件
- 全新的 Windows 应用程序 Icon
- Icon 同时用于 EXE、主窗口、任务栏与 Alt+Tab
- 直接拖放 Chrome 原生 `Bookmarks` 文件打开
- 批量输入网址，每行一个
- UTF-8 TXT 文件批量导入
- 对可接受的裸域名 / 路径自动补上 `https://`
- 任一输入行无效时阻止整批添加，并显示错误行号
- 每次批量添加作为一个 Undo / Redo 操作
- 顶部菜单、左侧文件夹树、右侧内容列表均提供批量添加入口
- 读取、浏览、搜索、编辑、移动、拖放、删除、批量操作
- Undo / Redo
- Chrome 书签 HTML 导入 / 导出
- 显式 Safe Save 安全保存
- 面向超大型书签库的性能与稳定性验证

正式 Release 只提供：

- `ChromeBookmarksManager.exe`
- `ChromeBookmarksManager.exe.sha256`

最新版本下载：

[前往 GitHub Releases](https://github.com/devin930906/Chrome-Bookmarks-Manager/releases/latest)

---

## 主要功能

### Chrome 原生书签支持

- 支持 Chrome 原生书签文件版本 `1`
- 支持 `bookmark_bar`、`other`、`synced` 三个根节点
- 支持多层嵌套文件夹与 URL 书签节点
- 保留原始子节点顺序与父子关系
- 精确统计书签数量和文件夹数量
- 保留 Chrome 原始时间戳字符串
- 支持对象格式与旧版序列化字符串格式的 `meta_info`
- 解析并在显式保存时重新生成 Chromium 兼容的 `checksum` 与 `checksum_sha256`
- 内存中保留未知或暂不支持的 JSON 属性
- 对格式错误或结构不安全的数据返回明确的类型化验证错误

### 大型书签库浏览

- 异步加载书签文件
- 支持取消加载
- 三根节点文件夹树
- 右侧显示当前选中文件夹的直属书签
- WPF TreeView / ListView 显式开启虚拟化与回收
- 适合大量文件夹与大量书签的滚动浏览

### 搜索

- 加载后建立单一内存搜索索引
- 支持名称搜索
- 支持 URL 搜索
- 支持域名文本搜索
- 大小写不敏感匹配
- 支持中文 / Unicode 子串搜索
- 支持：
  - `全部书签`
  - `当前文件夹`
- 250 ms 防抖
- 支持取消旧查询
- 最新查询优先，旧查询不会覆盖新结果
- 搜索结果可定位回原始父文件夹与原始书签对象

### 编辑

支持在内存中执行：

- 新增书签
- 新增文件夹
- 重命名普通文件夹
- 重命名书签
- 修改书签 URL
- 自动 Dirty 状态跟踪

在文档存在未保存修改时，重新加载或关闭程序会提供：

- 保存 / 放弃 / 取消
- 或对应场景中的放弃 / 取消保护

未明确保存之前，不会自动改写 Chrome 原始书签文件。

### 移动与拖放

- 书签可通过“移动到...”移动到任意有效文件夹
- 文件夹可通过“移动到...”调整位置
- 防止文件夹移动到自身
- 防止文件夹移动到自身的子孙节点
- 书签支持同文件夹 Before / After 重排
- 书签支持跨文件夹 Into 移动
- 文件夹拖放使用上 / 中 / 下区域判断 Before / Into / After
- Chrome 永久根节点可以作为目标，但不能作为移动源
- 重新排序时保持书签和文件夹的混合子节点顺序
- 纯移动操作保留节点对象身份、ID、GUID、元数据、时间戳、URL、子树与文档统计
- 搜索状态下跨文件夹移动后，搜索结果仍保持一致

### 删除与批量操作

- Delete 键删除
- 右键菜单删除
- 单个删除
- 批量删除
- Ctrl / Shift 多选
- Ctrl+A 选择当前显示结果
- 支持跨文件夹批量删除
- 自动去除重复选择
- 保留未删除项目的原有顺序
- 批量“移动到...”保持选中书签的顺序
- 删除确认默认使用安全选项
- 删除文件夹前显示递归 URL / 文件夹数量
- 永久根节点受到保护

### Undo / Redo

- 最大 200 步可撤销操作历史
- 覆盖：
  - 新增
  - 重命名
  - URL 修改
  - 移动
  - 排序
  - 批量移动
  - 单个删除
  - 批量删除
  - 文件夹递归删除
- Undo / Redo 保留节点身份与精确的混合子节点位置
- 保持文档统计与搜索索引一致
- 撤销回加载基线后恢复 Clean 状态
- 新的分叉编辑会清空 Redo 分支
- 快捷键：
  - Undo：`Ctrl+Z`
  - Redo：`Ctrl+Y`
  - Redo：`Ctrl+Shift+Z`

---

## Safe Save 安全保存

V0.9 开始加入对 Chrome 原生 `Bookmarks` 文件的显式保存能力。

设计原则是：

> **编辑可以自由，写入必须保守。**

保存不是自动执行的。只有用户明确点击“保存”或触发对应保存命令时，程序才会进入 Safe Save 流程。

### 保存前保护

- Chrome 必须完全关闭
- 检测到 `chrome.exe` 时禁止保存
- 加载时记录源文件基线：
  - SHA-256
  - 文件长度
  - `LastWriteTimeUtc`
- 如果源文件在外部被修改，程序会阻止覆盖并要求重新加载

### 写入流程

1. 先生成新的书签 JSON
2. 写入同目录唯一临时文件
3. 重新打开并逻辑验证临时文件
4. 创建应用程序自己的备份
5. 对备份进行 SHA-256 和长度验证
6. 再次确认原始源文件没有发生变化
7. 使用原子替换方式写回
8. 重新打开最终文件并再次验证
9. 成功后才把当前状态标记为已保存

应用程序备份格式类似：

```text
Bookmarks.ChromeBookmarksManager.YYYYMMDD-HHmmss.fff.bak
```

程序不会覆盖 Chrome 自己的 `Bookmarks.bak`。

如果在替换源文件之前发生失败，原始书签文件保持不变。

如果极少数情况下在替换后验证失败，已经验证过的应用程序备份会被保留，并在界面中提示可能需要恢复。

---

## 语言

V1.1 起支持完整的中英双语界面。

### 简体中文

首次启动默认：

```text
简体中文
```

### English

可在程序菜单：

```text
语言 → English
```

即时切换。

切换不需要重新启动程序。

语言选择会独立保存到当前 Windows 用户的应用配置目录，不会修改 Chrome 书签文件。

---

## 系统要求

- Windows 10 x64
- Windows 11 x64
- .NET 10
- WPF
- C#
- 自包含（Self-contained）
- Single-file EXE

普通用户运行正式 Release 时不需要另外安装 .NET Runtime。

---

## 下载与运行

前往：

[GitHub Releases](https://github.com/devin930906/Chrome-Bookmarks-Manager/releases)

下载：

```text
ChromeBookmarksManager.exe
```

可选下载：

```text
ChromeBookmarksManager.exe.sha256
```

用于验证文件完整性。

PowerShell 校验 SHA-256：

```powershell
Get-FileHash .\ChromeBookmarksManager.exe -Algorithm SHA256
```

将结果与 Release 中的 `ChromeBookmarksManager.exe.sha256` 比较即可。

---

## 构建

```powershell
dotnet restore ChromeBookmarksManager.slnx
dotnet build ChromeBookmarksManager.slnx --configuration Release --no-restore
```

---

## 测试

运行完整测试：

```powershell
dotnet test ChromeBookmarksManager.slnx --configuration Release
```

### ReaderScale

250,000 URL 大型读取测试：

```powershell
pwsh -NoProfile -File scripts/Measure-Reader.ps1 -UrlCount 250000
```

### BrowserScale

250,000 URL 浏览状态测试：

```powershell
pwsh -NoProfile -File scripts/Measure-BrowserState.ps1 -UrlCount 250000
```

### SearchScale

250,000 URL 搜索测试：

```powershell
pwsh -NoProfile -File scripts/Measure-Search.ps1 -UrlCount 250000
```

### MoveScale

准备 Release 时可运行更大的移动压力测试：

```powershell
$env:CBM_MOVE_URL_COUNT = "250000"
$env:CBM_MOVE_FOLDER_COUNT = "4000"
dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=MoveScale"
Remove-Item Env:CBM_MOVE_URL_COUNT
Remove-Item Env:CBM_MOVE_FOLDER_COUNT
```

### DeleteBatchScale

```powershell
$env:CBM_DELETE_URL_COUNT = "250000"
$env:CBM_DELETE_FOLDER_COUNT = "4000"
dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=DeleteBatchScale"
Remove-Item Env:CBM_DELETE_URL_COUNT
Remove-Item Env:CBM_DELETE_FOLDER_COUNT
```

### WriteScale

正常 CI 使用 10,000 个合成 URL：

```powershell
dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=WriteScale"
```

显式 250,000 URL Release 测量：

```powershell
$env:CBM_WRITE_URL_COUNT = "250000"
dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=WriteScale" --logger "console;verbosity=normal"
Remove-Item Env:CBM_WRITE_URL_COUNT
```

所有性能时间和内存数字只作为诊断参考，不作为正确性的硬性阈值。

---

## UI 与安全契约验证

```powershell
pwsh -NoProfile -File scripts/Verify-BrowserVirtualization.ps1
pwsh -NoProfile -File scripts/Verify-SearchUi.ps1
pwsh -NoProfile -File scripts/Verify-V05EditingUi.ps1
pwsh -NoProfile -File scripts/Verify-V06MoveUi.ps1
pwsh -NoProfile -File scripts/Verify-V06BookmarkDragDrop.ps1
pwsh -NoProfile -File scripts/Verify-V06FolderDragDrop.ps1
pwsh -NoProfile -File scripts/Verify-V06MoveSafety.ps1
node scripts/Verify-V07DeleteUi.mjs
pwsh -NoProfile -File scripts/Verify-V07DeleteSafety.ps1
pwsh -NoProfile -File scripts/Verify-RepositorySafety.ps1
```

生成的测试数据只使用保留的示例域名，不包含用户真实书签标题、URL、搜索词或原始书签文件内容。

---

## 发布

生成 Windows x64 单文件程序：

```powershell
pwsh -NoProfile -File scripts/Publish-Windows.ps1
pwsh -NoProfile -File scripts/Verify-SingleFilePublish.ps1 -PublishDirectory artifacts/win-x64
```

输出：

```text
artifacts/win-x64/ChromeBookmarksManager.exe
```

项目开启：

```text
IncludeNativeLibrariesForSelfExtract=true
```

WPF 所需的原生运行库会被打包进单个 EXE。运行时 .NET 可能会把必要的原生库解压到运行时临时位置。

---

## Smoke Test

```powershell
pwsh -NoProfile -File scripts/SmokeTest-Windows.ps1 -ExecutablePath artifacts/win-x64/ChromeBookmarksManager.exe
```

Smoke Test 会启动正式发布 EXE，并确认进程在观察时间内保持正常运行。

---

## GitHub Actions

`.github/workflows/build-windows.yml` 在 Windows Runner 上执行完整 CI。

主要包括：

1. 仓库隐私检查
2. Restore
3. Release Build
4. Chrome Bookmarks ReaderScale
5. 浏览器 UI 虚拟化契约
6. 搜索 UI 契约
7. V0.5 编辑 UI 契约
8. V0.6 Move UI 契约
9. V0.6 书签 Drag & Drop 契约
10. V0.6 文件夹 Drag & Drop 契约
11. V0.6 Move 安全检查
12. V0.7 Delete 安全检查
13. BrowserScale
14. SearchScale
15. MoveScale
16. DeleteBatchScale
17. Undo / Redo / Write 相关安全与规模测试
18. 完整 xUnit 测试
19. Windows x64 Self-contained Publish
20. Single-file 验证
21. EXE 启动 Smoke Test
22. EXE 版本验证
23. Artifact 上传

正常 CI 中：

- BrowserScale：10,000 个合成 URL
- SearchScale：10,000 个合成 URL
- MoveScale：10,000 个合成 URL + 1,000 个文件夹
- DeleteBatchScale：10,000 个合成 URL + 1,000 个文件夹

250,000 URL 级别的大型测量保留为显式 Release / Measurement 流程，避免永久拖慢正常 CI。

下载 Artifact 名称：

```text
ChromeBookmarksManager-win-x64
```

---

## 数据隐私

真实 Chrome `Bookmarks` 文件属于私人数据，禁止提交到本仓库。

Git 仓库中只允许：

- `samples/` 下的合成 fixture
- 保留示例域名
- 不含真实书签标题
- 不含真实 URL
- 不含私人搜索词
- 不含真实原始 `Bookmarks` 文件
- 不含真实备份文件

仓库安全检查会阻止典型生产书签文件、备份文件和私人数据目录被追踪提交。

---

## 大型书签库实机验证

项目曾在 Windows 10 实机上使用大型 Chrome 书签库进行验收：

- 209,382 个 URL
- 3,404 个文件夹
- V0.2 首次读取观察时间约 1.8 秒
- V0.3 浏览 UI 观察加载时间约 2.5 秒
- V0.4 搜索版本观察加载时间约 1.7 秒

这些时间是特定机器上的观察值，并不是所有 Windows 电脑的性能保证。

验收过程中重点验证：

- 文件夹树
- 大列表滚动
- Name 搜索
- URL / 域名搜索
- 中文 / Unicode 搜索
- 最新查询优先
- 搜索结果导航
- 新增 / 修改
- Move
- Drag & Drop
- Delete
- Batch
- Undo / Redo
- Safe Save
- Chrome 读取兼容性
- Chrome 重写后的再次读取与再次保存
- Chrome 运行时阻止保存
- 外部修改冲突阻止覆盖

在只读阶段，源文件的：

- SHA-256
- 文件长度
- LastWriteTimeUtc

均验证保持不变。

进入 V0.9 Safe Save 后，正式保存流程还额外验证了独立外部备份、应用程序备份、Chrome 正常读取/重写，以及第二次保存。

---

## 版本历史

### V0.1 — Bootstrap

已完成：

- 工程骨架
- 自动化测试
- 隐私保护
- GitHub Actions
- Single-file EXE

### V0.2 — Chrome Bookmarks Reader

已完成：

- Chrome 原生文件解析
- 验证
- 取消加载
- 元数据保留
- Windows 10 实机验收

### V0.3 — Browser UI

已完成：

- 文件夹树
- 当前文件夹书签列表
- 状态信息
- WPF 虚拟化
- 大型列表浏览

### V0.4 — Search / Index

已完成：

- 内存索引
- 名称 / URL 搜索
- 中文 / Unicode 搜索
- 搜索结果导航
- 并发查询取消
- SearchScale

### V0.5 — Editing

已完成：

- 新增
- 重命名
- URL 编辑
- Dirty 状态
- 未保存修改保护

### V0.6 — Move / Drag & Drop

已完成：

- Move to...
- 同文件夹排序
- 跨文件夹移动
- 文件夹层级保护
- MoveScale

### V0.7 — Delete / Batch

已完成：

- 单个删除
- 递归文件夹删除
- 多选
- 批量删除
- 批量移动
- DeleteBatchScale

### V0.8 — Undo / Redo

已完成：

- 最多 200 步历史
- Ctrl+Z
- Ctrl+Y
- Ctrl+Shift+Z
- Clean / Dirty checkpoint
- UndoRedoScale

### V0.9 — Safe Chrome Write

已完成：

- Chromium 兼容 checksum
- 确定性写入
- 外部修改检测
- Chrome 进程保护
- 已验证备份
- 原子替换
- 显式 Save
- WriteScale
- Windows 10 生产书签实机验收

### V1.0 — Stable

第一个稳定个人使用版本。

### V1.1 — 双语界面

已完成：

- 简体中文
- English
- 即时切换
- 语言记忆
- Windows 品牌 Icon

### V1.1.1 — Icon Refresh

已完成：

- 更新 Windows 应用程序 Icon
- EXE / 主窗口 / 任务栏 / Alt+Tab Icon
- 版本升级至 1.1.1
- 自动化 CI 验证
- Windows 实机视觉验收
- 正式 GitHub Release

### V1.2.0 — 原生拖放 + 批量导入

当前稳定版本。

已完成：

- 直接拖放 Chrome 原生 `Bookmarks` 文件打开
- 拒绝把无关外部文件误当成原生 Bookmarks
- 批量网址输入，每行一个
- UTF-8 TXT 导入
- 裸域名 / 路径自动补 `https://`
- 无效行提示与整批阻止机制
- 每次批量添加作为一个 Undo / Redo 操作
- 顶部“编辑”菜单、左侧文件夹树、右侧内容列表均可进入批量添加
- 完整 Windows CI 验证
- Windows 实机完成拖放、批量输入、TXT 校验、Save/Reopen、既有功能回归及真实大型书签只读加载验收

---

## 设计与实现文档

- [已批准的总体设计](docs/superpowers/specs/2026-09-19-chrome-bookmarks-manager-design.md)
- [V0.1 实现计划](docs/superpowers/plans/2026-09-19-v0.1-bootstrap.md)
- [V0.2 实现计划](docs/superpowers/plans/2026-09-19-v0.2-chrome-bookmarks-reader.md)
- [V0.3 实现计划](docs/superpowers/plans/2026-09-19-v0.3-browser-ui.md)
- [V0.4 实现计划](docs/superpowers/plans/2026-09-19-v0.4-search-index.md)
- [V0.5 实现计划](docs/superpowers/plans/2026-09-19-v0.5-editing.md)
- [V0.6 设计](docs/superpowers/specs/2026-09-20-v0.6-move-drag-drop-design.md)
- [V0.6 实现计划](docs/superpowers/plans/2026-09-20-v0.6-move-drag-drop.md)
- [V0.7 设计](docs/superpowers/specs/2026-09-20-v0.7-delete-batch-design.md)
- [V0.7 实现计划](docs/superpowers/plans/2026-09-20-v0.7-delete-batch.md)
- [V0.8 设计](docs/superpowers/specs/2026-09-20-v0.8-undo-redo-design.md)
- [V0.8 实现计划](docs/superpowers/plans/2026-09-20-v0.8-undo-redo.md)
- [V0.9 设计](docs/superpowers/specs/2026-09-20-v0.9-safe-chrome-write-design.md)
- [V0.9 实现计划](docs/superpowers/plans/2026-09-20-v0.9-safe-chrome-write.md)
- [V1.0 稳定版设计](docs/superpowers/specs/2026-09-21-v1.0-stable-release-design.md)
- [V1.0 实现计划](docs/superpowers/plans/2026-09-21-v1.0-stable-release.md)

---

## 开发原则

```text
Read broadly.
Edit explicitly.
Write conservatively.
Backup always.
Verify after writing.
```

对应中文：

```text
广泛读取。
明确编辑。
谨慎写入。
始终备份。
写入后验证。
```

**数据正确性与数据安全，优先于功能数量与视觉效果。**
