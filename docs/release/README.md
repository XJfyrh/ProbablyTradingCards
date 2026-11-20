# 发布《可能是集换卡牌》

用同一次构建准备 Steam 创意工坊内容和 GitHub 安装 ZIP。版本取自 [`manifest.xml`](../../ProbablyTradingCards/manifest.xml)。更新时保持模组 ID 不变，并按实际测试结果维护 `GameVersions`。

## 生成发布包

安装 PowerShell 7、.NET 10 SDK 和带 Pillow 的 Python。构建使用本机《Probably Stolen》正式游戏的 DLL。Python 可复用仓库的 `.venv`，也可通过 `-PythonExe` 指定。[构建环境](../../README.md#构建模组)

在仓库根目录运行以下命令。用 `-GameDir` 或环境变量 `PROBABLY_STOLEN_GAME_DIR` 指定实际的游戏目录。

```powershell
$gameDir = 'C:\Games\Probably Stolen'
./tests/verify.ps1 -GameDir $gameDir
./scripts/release.ps1 -GameDir $gameDir
```

脚本默认写入 `artifacts/releases/v<版本>-<时间>/`。用 `-OutputDir` 可以指定另一个尚不存在的目录。

| 输出 | 用途 |
| --- | --- |
| `ProbablyTradingCards/` | 工坊内容目录，根目录有 `manifest.xml`、`LICENSE` 和 `CREDITS.md`。 |
| `ProbablyTradingCards-v<版本>.zip` | GitHub Release 附件，解压后可把整个 `ProbablyTradingCards` 文件夹放进游戏的 `Mods`。 |
| `SHA256SUMS.txt` | 与 ZIP 一起上传，供玩家核对下载。 |
| `workshop-preview.png` | 默认工坊封面，和 VDF 一起保存在内容目录旁。 |
| `workshop.vdf` | 本机 SteamCMD 配置。若移动了输出目录，重新打包或更新 VDF 的绝对路径。 |

脚本从空目录生成正式包，校验清单和 DLL，并核对 ZIP 文件列表。默认使用《空间站传奇》的 Joe Wild 卡面作为工坊预览。可用 `-PreviewFile` 换成小于 1 MB 的 PNG、JPEG 或 GIF。[工坊预览要求](https://partner.steamgames.com/doc/api/ISteamUGC#SubmitItemUpdateResult_t)

先运行上面的 `verify.ps1` 生成扫描所需的依赖，再用本机游戏的工坊扫描规则检查本次正式包。将 `$gameDir` 和 `$content` 分别换成游戏安装目录和本次发布目录下的 `ProbablyTradingCards`。

```powershell
$content = '<发布目录>/ProbablyTradingCards'
dotnet run --project tests/Extensions/Extensions.csproj -c Release "-p:GameDir=$gameDir" -- --scan-workshop "$content" "$gameDir"
```

在专用测试档安装本次产出的正式包。按试玩步骤核对进货、拆包、收藏和保存重启，给每个检查点追加 `--release-dir '<发布目录>/ProbablyTradingCards'`，让工具比对本次的正式包。游戏内也可从 F8 控制台用 `mod-scan probably_trading_cards` 查看扫描结果。[官方扫描指南](https://github.com/dragonpan2/probably-stolen-modding/blob/main/ModdingGuide.md#checking-before-you-publish) · [正式包试玩](../../tests/README.md#用正式包跑一轮购买拆包与读档)

## Steam 创意工坊

用拥有《Probably Stolen》正式版的 Steam 账号启动 [SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD)。在它的交互提示符中执行以下命令，替换账号名与打包生成的 VDF 路径。登录时按 Steam 提示输入凭据和验证码。

```text
login <Steam账号名>
workshop_build_item "<发布目录的完整路径>\workshop.vdf"
quit
```

- 首次上传默认隐藏。SteamCMD 会将条目 ID 写回 VDF。通过条目页面完成首次发布所需的工坊协议确认，订阅并重启游戏核对下载结果。测试订阅时，把同 ID 的本地模组移出 `Mods`，否则游戏会优先加载本地包。
- 确认条目页面和实机加载后，在工坊页面调整公开可见性。工坊介绍也可从[首发文案](workshop-description.txt)复制。
- 发布更新时向脚本传入 `-WorkshopItemId <已有条目ID>`，使用生成的新 VDF。更新默认沿用该条目的可见性。确需调整时传入 `-Visibility Public`、`FriendsOnly` 或 `Hidden`，用 `-ChangeNote` 填写本次改动。 [官方工坊发布指南](https://github.com/dragonpan2/probably-stolen-modding/blob/main/ModdingGuide.md#publishing-with-steamcmd)

## GitHub 仓库与 Release

1. 首次发布使用干净源码目录。确认源码和美术的使用条款，然后在该目录运行 `git init -b main`。用 `git config user.name` 设置公开署名，并用 `git config user.email` 设置 GitHub 账号的 `noreply` 邮箱。[设置 Git 提交邮箱](https://docs.github.com/en/account-and-profile/how-tos/email-preferences/setting-your-commit-email-address)
2. 审查将发布的文件并提交。创建一个空的 GitHub 仓库，将其设为干净源码目录的远端并推送首发提交。[将本地代码添加到 GitHub](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github)
3. 将打包所用源码对应的提交标记为 `v<版本>`，在 GitHub 上用该标签新建 Release，上传安装 ZIP 和 `SHA256SUMS.txt`。写明适用游戏版本、安装方法和本版变化。[创建 GitHub Release](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
