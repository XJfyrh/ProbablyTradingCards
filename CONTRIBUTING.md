# 参与贡献

遇到 Bug 或有玩法建议，可以先在 [Issues](https://github.com/XJfyrh/ProbablyTradingCards/issues) 搜索。找不到相同问题时，在[反馈表单](https://github.com/XJfyrh/ProbablyTradingCards/issues/new/choose)选择对应类型。报告 Bug 时请写明版本、复现步骤和实际结果。涉及多个模组时，附上模组列表和加载顺序。

## 提交改动

- 文档修正和复现明确的小修复可以直接开 PR。涉及出率、供货、存档格式或扩展接口时，先在 Issue 里说明希望改变的行为，便于讨论玩法和兼容性。
- 构建模组时，按[构建说明](README.md#构建模组)配置本机游戏和工具。制作独立扩展可以从[卡辑示例](Examples/DockLetters/README.md)或[玩法示例](Examples/CollectionBounty/README.md)开始。
- 依照[测试与试玩](tests/README.md)选择受影响的检查，在 PR 中写明运行过的命令、结果和仍待验证的部分。改卡面时附上预览。改交互时说明实机复现步骤。
- 保留已发布的卡辑键、卡牌编号和贴图键，具体要求见[新增卡辑](docs/development/新增卡辑.md)和[物品与存档](docs/development/物品与存档.md)。提交美术改动时说明来源和使用许可。请勿提交原版游戏程序集、本机存档或构建产物。

每个 PR 都会自动测试玩法与存档规则、卡辑作者工具和 Python 脚本。检查还会对照发布标签核对卡辑标识，并把重新导出的主模组贴图与提交的成品逐张比较。模组构建、扩展集成和实机试玩需要本机游戏。[GitHub Actions](https://github.com/XJfyrh/ProbablyTradingCards/actions/workflows/checks.yml)

## 维护代码注释

从仓库根目录运行以下命令，检查未提交改动中的注释。检查已提交的分支改动时，将 `--base` 设为分支与目标分支的共同祖先。CI 对 PR 的合并结果使用目标分支提交作基线，对推送使用推送前的提交。

```powershell
python -m pip install -r scripts/requirements-comments.txt
python scripts/check_comments.py
```

检查会指出新增或改写的说明性注释及其行号。基线中已有的注释可以分批处理。解释设计取舍时，将触发条件、原因和验证入口写入实现决策文档。审查发现用注释掩盖的实现缺口时，为该缺口建 Issue。盘点全库可运行 `python scripts/check_comments.py --list`，再按仓库技能逐条分类。[实现决策](docs/development/实现决策.md) · [delete-comment 技能](.agents/skills/delete-comment/SKILL.md)
