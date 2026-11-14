# AGENTS.md — PCL Reborn 项目代理守则

> 作用域：本仓库 `F:\MCProject\PCL`（PCL Reborn）。用户全局规则见 `~/.dsh/AGENTS.md`，冲突时以本文件为准。

## 项目速览

- `Plain Craft Launcher 2\`：VB.NET + WPF 主程序（.NET Framework 4.8），入口 `FormMain.xaml.vb`、`Modules\ModMain.vb`。
- `PCLCS\`：C# 核心库（启动、Minecraft、资源），入口 `Launch\LaunchUtils.cs`、`Minecraft\MCUtils.cs`。
- `MeloongCore\`：独立基础库（`Shared\` = .NET Standard 2.0，`Wpf\`/`Tests\` = .NET Framework 4.8）。
- 解决方案 `Plain Craft Launcher 2.sln`；CI 见 `.github/workflows/build.yml`（windows-latest，Debug + Release）。
- 沟通用中文；文本一律 UTF-8 无 BOM。
- 代码风格与分层约定见 `MeloongCore\AGENTS.md` 与会话内 `MeloongCore\.agents\仓库指示.md`。

## 上下文压缩 — billion-context（MUST follow）

- **禁止调用 DSH 自带的原生压缩**：当前预设下会 100% 抛 `error[PTC-X001]`，不要尝试、不要重试、不要用「先试一下」验证。
- 需要压缩时只走 billion-context。本会话暴露的 `compress` 工具由 billion-context 提供（工具描述自述 "Context management is provided by billion-context"），实测可用；要禁的是绕开它的 DSH 原生/自动压缩路径。
- 一次调用把**所有**要折叠的区间写进一个 `content` 字符串：每个块以「本次 status 返回的 ref 区间」开头，后接自包含摘要。不要把多个区间拆成多次调用。
- 手工经 HTTP 代理压缩时（bili 实例）：
  1. 先取 `conversationId`：`GET http://127.0.0.1:18789/__bili/plugin/status?...`。注意本机实例对 status 路由**要求** `conversationId` 参数，缺失会返回 `400 {"ok":false,"error":"conversationId query parameter is required"}`，只给 `fallback=latest` 不够。
  2. `POST /__bili/plugin/tool`，body = `{ conversationId, tool: "compress", args: { content } }`。
  3. `content` 的块头只用**当前** status 返回的 ref；旧 nudge、历史 ref 一律不用。
- 成功判据：`outcome="applied"` 且 `blocksCreated >= 1`。失败先重读 status 再组块，不重试旧 ref。
- 代理地址优先读环境变量 `BILLION_CONTEXT_PROXY`；当前未设置，默认 `http://127.0.0.1:18789`（端口可能随实例变化）。
- 摘要必须自包含：写清用户目标与意图、决策与理由、`路径:行号`、错误原文、未完成事项；不要只写「已探索/已完成」。

## 功能改动复核 — review-agent（MUST follow）

- 任何功能或行为改动（代码、内置数据生成、CI 采集管线）完成后，提交前必须在 subagent 中加载 `review-agent` 技能执行一次独立复核。
- 复核只给「目标 + 路径 + 期望不变量 + 可执行判据」，不喂结论；复核发现的必修问题修完后必须再复核一轮。
- 复核之外另做功能冒烟（真实程序或等价 harness）；两者都通过才允许提交、打 tag 或发布。
