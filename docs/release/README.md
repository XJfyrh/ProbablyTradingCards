# 发布《可能是集换卡牌》

用同一次构建准备 Steam 创意工坊内容和 GitHub 安装 ZIP。版本取自 [`manifest.xml`](../../ProbablyTradingCards/manifest.xml)。更新时保持模组 ID 不变，并按实际测试结果维护 `GameVersions`。

试玩记录使用游戏主菜单的版本。正式版 100-Rev13 的模组菜单仍按 Unity 字段 `0.46D` 识别兼容性，100-Rev14 起用实际游戏版本匹配 `1.0.0` 前缀。支持 Rev13 时在清单中保留旧识别项。[官方版本匹配规则](https://github.com/dragonpan2/probably-stolen-modding/blob/main/ModdingGuide.zh-CN.md#游戏版本)

## 生成发布包

安装 PowerShell 7 和 .NET 10 SDK，按离线测试说明给 Python 安装图像和注释解析依赖。构建使用本机《Probably Stolen》正式游戏的 DLL。Python 可复用仓库的 `.venv`，也可通过 `-PythonExe` 指定。[构建环境](../../README.md#构建模组) · [离线测试环境](../../tests/README.md#构建模组并运行离线测试)

在仓库根目录运行以下命令。用 `-GameDir` 或环境变量 `PROBABLY_STOLEN_GAME_DIR` 指定实际的游戏目录。

```powershell
$gameDir = 'C:\Games\Probably Stolen'
./tests/verify.ps1 -GameDir $gameDir
./scripts/release.ps1 -GameDir $gameDir
```

`release.ps1` 默认写入 `artifacts/releases/v<版本>-<时间>/`。用 `-OutputDir` 可以指定另一个尚不存在的目录。准备工坊更新时，在这条打包命令上加 `-ChangeNote '本版变化'`。工坊介绍取自仓库中的文案，已有条目默认沿用当前可见性。需要更改时传入 `-Visibility`（`Public`、`FriendsOnly` 或 `Hidden`）。[工坊文案](workshop-description.txt)

| 输出 | 用途 |
| --- | --- |
| `ProbablyTradingCards/` | 工坊内容目录，根目录有 `manifest.xml`、`LICENSE` 和 `CREDITS.md`。 |
| `ProbablyTradingCards-v<版本>.zip` | GitHub Release 附件，解压后可把整个 `ProbablyTradingCards` 文件夹放进游戏的 `Mods`。 |
| `SHA256SUMS.txt` | 与 ZIP 一起上传，供玩家核对下载。 |
| `workshop-preview.png` | 默认工坊封面，和 VDF 一起保存在内容目录旁。 |
| `workshop.vdf` | 本机 SteamCMD 配置。若移动了输出目录，重新打包或更新 VDF 的绝对路径。 |

脚本从空目录生成正式包，校验清单和 DLL，并核对 ZIP 文件列表。默认使用《空间站传奇》的 Joe Wild 卡面作为工坊预览。可用 `-PreviewFile` 换成小于 1 MB 的 PNG、JPEG 或 GIF。[工坊预览要求](https://partner.steamgames.com/doc/api/ISteamUGC#SubmitItemUpdateResult_t)

运行上面的 `verify.ps1` 生成扫描所需的依赖后，用本机游戏的工坊扫描规则检查发布包。沿用上面的 `$gameDir`，将 `$content` 指向本次发布目录下的 `ProbablyTradingCards`。

```powershell
$content = '<发布目录>/ProbablyTradingCards'
dotnet run --project tests/Extensions/Extensions.csproj -c Release "-p:GameDir=$gameDir" -- --scan-workshop "$content" "$gameDir"
```

把本次生成的正式包安装到游戏，载入专用测试档。按试玩步骤核对进货、拆包、收藏和重启后的读档结果。每次记录检查点时都加上 `--release-dir "$content"`，让工具与本次发布包比对。游戏内也可从 F8 控制台用 `mod-scan probably_trading_cards` 查看扫描结果。[官方扫描指南](https://github.com/dragonpan2/probably-stolen-modding/blob/main/ModdingGuide.md#checking-before-you-publish)、[正式包试玩](../../tests/README.md#用正式包跑一轮购买拆包与读档)

## Steam 创意工坊

生成的 `workshop.vdf` 默认指向本模组已有的工坊条目 `3842861920`。上传时启动 SteamCMD，使用拥有《Probably Stolen》正式版的账号登录。将下列命令中的账号名和 VDF 路径换为实际值，登录后运行 `workshop_build_item` 更新条目。[SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD)

```text
login <Steam账号名>
workshop_build_item "<发布目录的完整路径>\workshop.vdf"
quit
```

上传后，把同 ID 的本地模组移出 `Mods`，订阅并重启游戏，核对实际加载的工坊版本。[官方工坊发布指南](https://github.com/dragonpan2/probably-stolen-modding/blob/main/ModdingGuide.md#publishing-with-steamcmd)

## GitHub Release

在打包所用的提交上创建与模组版本一致的 `v<版本>` 标签。在 GitHub 上用该标签新建 Release，上传同一次构建生成的安装 ZIP 和 `SHA256SUMS.txt`，并写明适用游戏版本、安装方法和本版变化。[创建 GitHub Release](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
