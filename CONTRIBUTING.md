# 参与贡献

遇到 Bug 或有玩法建议，可以先在 [Issues](https://github.com/XJfyrh/ProbablyTradingCards/issues) 搜索。找不到相同问题时，在[反馈表单](https://github.com/XJfyrh/ProbablyTradingCards/issues/new/choose)选择对应类型。报告 Bug 时请写明版本、复现步骤和实际结果。涉及多个模组时，附上模组列表和加载顺序。

## 提交改动

- 文档修正和复现明确的小修复可以直接开 PR。涉及出率、供货、存档格式或扩展接口时，先在 Issue 里说明希望改变的行为，便于讨论玩法和兼容性。
- 构建模组时，按[构建说明](README.md#构建模组)配置本机游戏和工具。制作独立扩展可以从[卡辑示例](Examples/DockLetters/README.md)或[玩法示例](Examples/CollectionBounty/README.md)开始。
- 依照[测试与试玩](tests/README.md)选择受影响的检查，在 PR 中写明运行过的命令、结果和仍待验证的部分。改卡面时附上预览。改交互时说明实机复现步骤。
- 保留已发布的卡辑键、卡牌编号和贴图键，具体要求见[新增卡辑](docs/development/新增卡辑.md)和[物品与存档](docs/development/物品与存档.md)。提交美术改动时说明来源和使用许可。请勿提交原版游戏程序集、本机存档或构建产物。

每个 PR 都会自动测试玩法与存档规则、卡辑作者工具和 Python 脚本。检查还会对照发布标签核对卡辑标识，并把重新导出的主模组贴图与提交的成品逐张比较。模组构建、扩展集成和实机试玩需要本机游戏。[GitHub Actions](https://github.com/XJfyrh/ProbablyTradingCards/actions/workflows/checks.yml)

## 维护代码注释

在仓库根目录用 PowerShell 运行。以下命令使用 Python 创建虚拟环境并安装解析依赖，CI 使用 Python 3.14。已有 `.venv` 时从第二行开始。

```powershell
python -m venv .venv
./.venv/Scripts/python.exe -m pip install -r scripts/requirements-comments.txt
```

检查尚未提交的改动时，默认与 `HEAD` 比较，未被 Git 忽略的新文件也会纳入检查。

```powershell
./.venv/Scripts/python.exe scripts/check_comments.py
```

检查准备合入 `main` 的整个分支时，先更新本地的 `origin/main` 引用，再与共同祖先比较。这样也会检查分支上已经提交的注释。

```powershell
git fetch origin main
$base = git merge-base HEAD origin/main
./.venv/Scripts/python.exe scripts/check_comments.py --base $base
```

通过时输出 `PASS: no new or edited prose comments.`。发现新增或改写的普通注释、C# XML 文档注释时，检查会打印文件、行号和原文，以状态码 1 退出。CI 检查 PR 的合并结果时，以目标分支提交为基线。已有注释可以分批处理。

盘点全库使用 `./.venv/Scripts/python.exe scripts/check_comments.py --list`。逐条审查时，删除赘述和过时说明，将设计理由写入实现决策文档，为注释掩盖的实现或测试缺口建立或关联 Issue。[实现决策](docs/development/实现决策.md) · [delete-comment 技能](.agents/skills/delete-comment/SKILL.md)
