# 美术导出工具

在仓库根目录用 PowerShell 运行。改一辑原画时，导出对应系列即可。卡框、参考样张和源文件位置见[美术规范](../docs/art/美术规范.md)。

## 导出新一辑

Python 导出工具使用 Pillow。首次使用可在仓库根目录运行以下命令安装美术工具依赖。[依赖版本](requirements.txt)

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r scripts/requirements.txt
```

作为独立模组发布时，使用《码头来信》的作者模板。`dotnet build` 会校验清单、生成纹理并打包。卡面和三档图标来自扩展包，默认包装、阅读时的磨损层和册页来自前置包。[独立模组模板与构建说明](../Examples/DockLetters/README.md)

在主项目内加一辑时，可以把[示例清单和 Art 文件夹](../Examples/DockLetters/Series/dock_demo)放在 `ProbablyTradingCards/Series/dock_demo/`。每张 PNG 应包含完整卡面和卡框，画幅为 2:3。

```powershell
.\.venv\Scripts\python.exe scripts/export_series.py dock_demo
```

脚本从每幅原画生成一张 512×768 阅读图和三张 16×32 背包图标。成品分别写入 `ProbablyTradingCards/Textures/Cards` 和 `Textures/Items`。默认沿用现有卡包和传奇收藏簿的图标。要自定义包装，在清单中设置 `"customPackArt": true` 并在同目录放入 `pack.png`。收藏簿对应 `"customAlbumArt": true` 和 `album.png`。包装原画使用透明背景，脚本会裁掉边缘并居中缩放到 32×48。

提交时带上清单、原画和生成的游戏纹理。下列命令只读检查主项目内新增卡辑的阅读图、三种品相图标、包装和共用图层，报告缺文件或尺寸错误。项目只有内置的夜话、传奇时，输出为 `No expansion manifests found.`。命令末尾加 `--project <模组项目目录>` 可检查另一份项目副本。[导出与检查脚本](export_series.py)

```powershell
.\.venv\Scripts\python.exe scripts/export_series.py --check
```

检查已经构建的独立示例时，指定前置发布包和该扩展的纹理目录。命令按作者工程的清单核对成品。

```powershell
.\.venv\Scripts\python.exe scripts/export_series.py --project Examples/DockLetters --base-package ProbablyTradingCards/dist/ProbablyTradingCards --output Examples/DockLetters/dist/DockLetters/Textures --check
```

## 替换《空间站夜话》卡面

将完好卡面放在 `ProbablyTradingCards/Textures/Cards/card_0.png` 至 `card_8.png` 的对应位置，再依次生成品相变体、物品图标和预览。

```powershell
./scripts/export_card_conditions.ps1
./scripts/export_card_icons.ps1
./scripts/preview_card_art.ps1
```

第一个脚本将两张磨损层叠在九张完好卡上，生成 18 张变体。第二个脚本从这 27 张卡面导出 16×32 的背包图标。打开 `artifacts/art-preview/preview-all.png` 查看九张完好卡面与图标的对照，`preview-rarity.png` 并排显示 C、R、SR 三个示例。卡包和集换册图标直接保存在 `Textures/Items/pack.png` 和 `album.png`。

这三个脚本使用 Windows 的 `System.Drawing`，可通过 `-ProjectRoot` 指定模组项目副本。预览另支持 `-OutputDirectory`。[磨损合成](export_card_conditions.ps1)、[图标导出](export_card_icons.ps1)、[预览生成](preview_card_art.ps1)

## 导出《空间站传奇》

替换常规卡原图后，确认所属批次的 `manifest.json` 指向实际采用的图片，再运行导出脚本。

```powershell
.\.venv\Scripts\python.exe ./scripts/export_station_legends.py
```

脚本读取四十张常规卡、十二幅异画原图、包装和收藏簿素材，将游戏用图写入 `ProbablyTradingCards/Textures/Cards` 和 `Textures/Items`。传奇的大卡面在游戏里叠加磨损层，背包用的三种品相图标在导出时合成。十一张异画中，Eleanor 的一个版本可以切换两幅画面，因此需要十二幅原图。[导出脚本](export_station_legends.py)

## 核对提交的游戏贴图

在 Windows 上确认能调用 PowerShell 7（`pwsh`），然后运行 `./.venv/Scripts/python.exe scripts/check_art_exports.py`。脚本在临时目录重新导出主模组各辑的派生贴图，逐张核对仓库成品的尺寸和 RGBA 像素。成功时显示核对张数，像素不一致时给出成品路径。夜话的完好卡面、磨损层和包册图标直接作为导出输入。独立扩展在自己的构建过程中从原画生成贴图。

## 修改共用磨损层

轻磨和折痕的源图分别是 `ProbablyTradingCards/Textures/Cards/wear_light.png` 和 `wear_creased.png`。改动其中一张后，按以下顺序更新使用这套磨损效果的卡面和图标。

1. 运行上文的夜话品相及图标导出命令，再运行传奇导出命令。传奇脚本同时更新游戏查看卡面使用的两张透明磨损层。对于直接加入主项目的扩展辑，逐辑运行 `.\.venv\Scripts\python.exe scripts/export_series.py <key>`，将 `<key>` 换成实际系列键。
2. 运行 `dotnet build ProbablyTradingCards/ProbablyTradingCards.csproj -c Release`，生成含新磨损层的前置发布包。
3. 独立扩展使用这个新前置包重新构建。其物品图标在构建时合成，而放大卡面的磨损由游戏从前置包读取。[指定构建使用的前置包](../Examples/DockLetters/README.md)
