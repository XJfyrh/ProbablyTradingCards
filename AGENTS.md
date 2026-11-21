# AGENTS.md

## 仓库入口

这是《Probably Stolen》的集换卡牌模组。模组代码在 `ProbablyTradingCards/`，卡辑清单、原画和游戏纹理分别在该目录的 `Series/`、`Art/` 和 `Textures/`。`Examples/DockLetters/` 和 `Examples/CollectionBounty/` 分别演示独立卡辑和玩法扩展。先从 [README](README.md) 找到与任务相关的文档和脚本。

## 执行与协作

- 从当前请求和已确认的上下文确定范围。可逆的细节用合理假设推进，并在影响结果时说明。遇到会改变交付、已有存档或发布状态的未决问题，先完成能独立推进的工作，再拿具体结果请用户决定。
- 技能用于辅助完成任务。用户的明确要求优先于技能中的工作流建议。若某条指南导致暂停或缩小范围，指出具体文件、对应条款和实际影响。
- 沿用现有入口和逻辑，保留调用方依赖的行为。能用类型、清单校验或构建检查稳定发现的错误，就在对应入口处理。
- 工具支持并行委托且能节省时间或提升质量时，将可独立验收的调查或验证分给子代理，明确范围和交付物，由主代理整合。汇报先写结果，再说明实测范围、关键假设和仍待验证的部分。

## 构建与验证

从仓库根目录用 PowerShell 执行。Smoke 使用 .NET 10 SDK，美术工具使用带 Pillow 的 Python。构建模组或验证游戏原生行为时，按构建说明设置 `PROBABLY_STOLEN_GAME_DIR`。[构建说明](README.md#构建模组)

| 改动范围 | 验证入口 |
| --- | --- |
| 模组代码或打包内容 | `dotnet build ProbablyTradingCards/ProbablyTradingCards.csproj -c Release` |
| 卡池、出率、供货或保存数据 | `dotnet run --project tests/Smoke/Smoke.csproj -c Release` |
| 卡辑注册、扩展接口或跨模组成交 | `./tests/Extensions/run.ps1` |
| 卡面或图标 | 按[美术导出说明](scripts/README.md)重新导出。主模组运行 `./.venv/Scripts/python.exe scripts/check_art_exports.py` 核对成品，独立扩展运行自身的构建命令。 |
| 鼠标交互、界面或完整读档流程 | 在专用测试档按[测试与试玩](tests/README.md)复验受影响的路径 |

按改动选检查。纯文档改动检查链接和命令引用。准备发布时运行 `./tests/verify.ps1`，并按试玩文档验证本次正式包。离线测试、原生验证包构建和实机试玩分别记录结果。已有检查能覆盖时直接复用，遇到失败、新改动或明确的覆盖缺口时再扩大验证。

## Code Review Rules

- 审查代码注释时使用仓库内的 [delete-comment 技能](.agents/skills/delete-comment/SKILL.md)。修改涉及原版游戏行为或验证工具的假设时，核对[实现决策](docs/development/实现决策.md)中对应的约束。
- 新增卡或卡辑时按[新增卡辑](docs/development/新增卡辑.md)更新标识清单。有意改变已发布存档格式时，说明如何读取旧物品和原封包，并用旧版数据验证。[发布标识检查](tests/README.md#核对已发布的卡辑标识)
- 标出改动跨模组公开接口或注册流程时对现有扩展的具体影响。以[外部模组接口](docs/development/外部模组接口.md)和独立扩展的验证结果核对兼容性。
- 标出正式包夹带原版游戏程序集或原生验证代码的改动。使用 [发布指南](docs/release/README.md)中的 `./scripts/release.ps1` 生成和检查发布包，按实测更新 `GameVersions`。在任务包含发布时执行上传或调整可见性。
