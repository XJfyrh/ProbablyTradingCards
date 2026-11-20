# 码头来信

《码头来信》是《可能是集换卡牌》的独立扩展，每包两张卡，配有专属收藏簿。首张必为 C 卡「末班信差」，第二张有 25% 的概率为 R 卡「返程船票」。末班信差的题材为人物，可以交给《收藏悬赏》的委托人。

## 安装试玩

将前置模组的 `ProbablyTradingCards` 文件夹和本扩展的 `DockLetters` 文件夹分别放入游戏的 `Mods` 目录，在模组菜单启用两者，按提示重启。第 2 天起，每周二可从发行商货单选择《码头来信》，买一包卡和一本收藏簿。拆包后应得到两张卡，右键阅读可看大图，也可将卡拖入册子的同名卡位。

## 在独立仓库制作卡辑

准备 .NET 10 SDK、Python 和本机游戏，使用与前置模组配套的作者模板。构建需要游戏 DLL，以及前置发布包中的 `ProbablyTradingCards.dll` 和 `Textures` 目录。下面在作者项目里安装图像导出所需的 Pillow。

在主仓库根目录导出模板。`TemplateDir` 填写尚未创建的目录，命令会将示例清单、原画、清单检查器和导出脚本一起复制进去。

```powershell
$templateDir = Join-Path (Get-Location).Path '../MyCardSet'
dotnet msbuild Examples/DockLetters/DockLetters.csproj -t:ExportTemplate "-p:TemplateDir=$templateDir"
Set-Location -LiteralPath $templateDir
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install Pillow
```

1. 给自己的作品确定独立的模组 ID 和 DLL 名称。在 `manifest.xml` 修改 `ID`、显示名、作者、版本和描述。将 `DockLetters.csproj` 改成自己的项目名，例如 `HarborPost.csproj`，并将 `manifest.xml` 的 `<Assembly>` 改为同名的 `HarborPost.dll`。保留 `probably_trading_cards` 作为前置依赖。
2. 修改 `Series/dock_demo/series.json` 的系列名、发售日、包价和卡位权重。每辑的清单都放在 `Series/<key>/series.json`，其中 `key` 与目录名相同。每张卡填写 `art`、`name`、`rarity`、`value` 和 `flavor`。人物卡添加 `"subject": "Character"`。
3. 把每张含卡框的 2:3 PNG 放到该辑的 `Art/<art>.png`。自绘包装可在清单设置 `customPackArt` 或 `customAlbumArt` 为 `true`，把透明背景的 `pack.png` 或 `album.png` 放在清单旁。默认使用前置模组的包装和册子图标。
4. 在作者项目目录运行下列命令，将 `GameDir` 换为本机游戏安装目录。默认使用其中的 `Mods/ProbablyTradingCards` 作为前置包。如果前置在其他位置，在命令末尾追加 `"-p:TradingCardsDll=前置发布包的完整路径/ProbablyTradingCards.dll"`。独立项目优先使用自身的 `.venv/Scripts/python.exe`，也可用 `-p:PythonExe=...` 指定解释器。

```powershell
dotnet build -c Release "-p:GameDir=C:/Games/Probably Stolen"
```

原样构建示例时，清单检查器会显示 `dock_letters/dock_demo · 2 张卡 · 第 2 天发售`。构建成功后，`dist/DockLetters/` 包含 `manifest.xml`、嵌有系列清单的 DLL、两张阅读图和六张背包图标。改名为 `HarborPost.csproj` 后，分发目录相应变为 `dist/HarborPost/`。

首次分发自己的卡辑前，在上述构建命令后加 `-p:UpdateSeriesIdentities=true`，生成并提交 `series-identities.json`。有这份清单后，普通构建会检查系列键、卡种编号、贴图键和包内张数。替换某张已分发单卡的画面时，沿用它的素材键。新增卡牌时生成新清单，查看差异后与卡辑一起提交。

## 在本仓库直接构建

```powershell
dotnet build ProbablyTradingCards/ProbablyTradingCards.csproj -c Release
dotnet build Examples/DockLetters/DockLetters.csproj -c Release
```

本仓库中的示例使用根目录的 `scripts` 和 Python 环境。装好 Pillow 即可生成游戏纹理。完整示例在 [系列清单](Series/dock_demo/series.json) 和 [注册入口](ModEntry.cs)。末班信差的示例卡面取自《空间站夜话》的巷口跑腿，返程船票使用《空间站传奇》的食品券卡面。创作自己的卡辑时可直接替换这两张原画。
