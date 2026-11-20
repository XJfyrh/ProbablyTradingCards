# 收藏悬赏

星港人物志编辑要收三张人物卡。每张按正常售价成交，卖出第三张时另得 120 信用点。三张可以同款，也可以来自不同作者。

将《可能是集换卡牌》和本模组分别装进游戏的 `Mods`，在模组菜单启用两者。编辑从第 3 天起每周三来访。把卡拿到柜台卖给他，可以分几周交齐。收齐后，编辑在这个存档里就不再来访。

搭配《码头来信》试玩时，第 2 天从发行商买码头卡包，第 3 天把开出的“末班信差”卖给编辑。三张人物卡可任选，编辑会告诉你还差几张。“返程船票”属于道具卡。

## 用作玩法模组模板

从仓库根目录运行以下命令，将玩法源码、项目文件和模组清单导出到尚未创建的目录。

```powershell
$templateDir = Join-Path (Get-Location).Path '../MyBounty'
dotnet msbuild Examples/CollectionBounty/CollectionBounty.csproj -t:ExportTemplate "-p:TemplateDir=$templateDir"
```

在新目录中，将 `CollectionBounty.csproj` 改成自己的项目名，例如 `MyBounty.csproj`。在 `manifest.xml` 填写自己的模组 ID，并把 `<Assembly>` 改为对应的 DLL 名，例如 `MyBounty.dll`。构建时传入你自己的游戏安装目录。

```powershell
Set-Location -LiteralPath $templateDir
dotnet build -c Release "-p:GameDir=C:/Games/Probably Stolen"
```

默认从游戏的 `Mods/ProbablyTradingCards` 引用前置。要引用其他位置的发布包，在构建命令末尾加 `"-p:TradingCardsDll=前置 DLL 的完整路径"`。分发 `dist/<项目名>` 整个目录。买家标识和存档键按模组 ID 区分。[模组清单](manifest.xml)

在 `DeliverySlot` 改收购条件。公开接口 `TradingCardsApi.TryGetCard` 从游戏物品取出卡牌定义和当前品相。可以按题材、稀有度、品相或 `card.Series.OwnerId` 筛选。示例只匹配人物题材，因此其他作者新注册的人物卡也能交付。

在作者工程的 `ModEntry.cs` 中，`QueueEditor` 决定来访日，`RecordDelivery` 发放奖励。原版在成交时调用买家的 `onItemBought`，预览和撤回报价只会查询收购条件。示例把依次交付的实体 ID 存在当前存档里，同一个成交回调重复触发只计一次，已收齐的编辑也会结束本次收购。
