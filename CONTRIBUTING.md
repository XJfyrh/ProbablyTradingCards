# 参与贡献

遇到 Bug 或有玩法建议，可以先在 [Issues](https://github.com/XJfyrh/ProbablyTradingCards/issues) 搜索。找不到相同问题时，在[反馈表单](https://github.com/XJfyrh/ProbablyTradingCards/issues/new/choose)选择对应类型。报告 Bug 时请写明版本、复现步骤和实际结果。涉及多个模组时，附上模组列表和加载顺序。

## 提交改动

- 文档修正和复现明确的小修复可以直接开 PR。涉及出率、供货、存档格式或扩展接口时，先在 Issue 里说明希望改变的行为，便于讨论玩法和兼容性。
- 按 [README](README.md#构建模组) 配置本机游戏和构建环境。制作独立扩展可以从[卡辑示例](Examples/DockLetters/README.md)或[玩法示例](Examples/CollectionBounty/README.md)开始。
- 依照[测试与试玩](tests/README.md)选择受影响的检查，在 PR 中写明运行过的命令、结果和仍待验证的部分。改卡面时附上预览。改交互时说明实机复现步骤。
- 保留已发布的卡辑键、卡牌编号和贴图键，具体要求见[新增卡辑](docs/development/新增卡辑.md)和[物品与存档](docs/development/物品与存档.md)。提交美术改动时说明来源和使用许可。请勿提交原版游戏程序集、本机存档或构建产物。

[GitHub Actions](https://github.com/XJfyrh/ProbablyTradingCards/actions/workflows/checks.yml)会检查 Python 工具和卡牌纹理。模组构建和实机检查需要本机游戏。
