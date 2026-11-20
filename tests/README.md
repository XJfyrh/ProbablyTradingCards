# 测试与试玩

改卡池、出率或供货日程时，先跑 Smoke。改卡辑注册或跨模组交易时，运行两个扩展的集成测试。要检查游戏中的库存、柜台或存取行为，安装验证包后可从 CLI 或 F9 运行同一套原生检查。以下命令均在仓库根目录的 PowerShell 中执行，先按构建说明设置 `PROBABLY_STOLEN_GAME_DIR`。[构建环境](../README.md#构建模组)

## 运行全部离线检查

```powershell
./tests/verify.ps1
```

脚本依次运行 Python 测试、Smoke 和独立扩展集成测试，再构建游戏内验证包。扩展集成测试也会先构建和检查正式包。可用 `-GameDir '游戏目录'` 和 `-PythonExe '解释器路径'` 指定环境。运行成功后，游戏内行为可按下文在专用测试档复验。[构建环境](../README.md#构建模组)

## 验证两个独立扩展

```powershell
./tests/Extensions/run.ps1
```

需要 .NET 10 SDK、本机游戏 DLL 和装有 Pillow 的 Python。可用 `-GameDir '游戏目录'` 指定游戏位置，用 `-PythonExe '解释器路径'` 选择 Python。脚本依次构建前置和两个扩展，核对构建扩展前后的前置 DLL 哈希，并用原版扫描器检查三个包。通过后会输出以 `PASS: add-on packages` 开头的结果。

离线宿主启用两个扩展的正式 DLL，用原版物品目录检查两个作者的同名卡辑能共存，读回《码头来信》的原封包，并通过原版买家的成交回调交付三张人物卡。检查包括预览、道具卡、重复回调、恢复部分进度、单次发奖和禁用清理。游戏窗口、鼠标输入和完整存档流程在试玩中验证。[测试入口](Extensions/Program.cs) · [成交回归](Extensions/BountyChecks.cs)

## 用 Smoke 检查数值与保存内容

```powershell
dotnet run --project tests/Smoke/Smoke.csproj -c Release
```

需要 .NET 10 SDK 及游戏自带的 `Newtonsoft.Json.dll`。游戏装在其他位置时，在命令末尾加 `"-p:GameDir=游戏安装目录"`。成功时控制台打印 `PASS`、检查数和已注册的系列键。[测试项目](Smoke/Smoke.csproj) · [检查项](Smoke/Program.cs)

- 《空间站夜话》用 2,000 个固定种子分别生成混合货源包和原厂包，检查 JSON 往返、手撕损伤、刀具保留品相和重试结果。逐一输入 0–99 的随机值来核对出率、二封概率和三种货源的品相边界。
- 《空间站传奇》检查四十张基础卡的分档、十一张异画、散货商的供货比例、整盒保底、内包存取，以及异画的开放窗口、命中边界和跨日重试。
- 客流与供货检查覆盖散货商第 20、50 天的卡包数量变化、发行商每周二到货，以及夜班收藏客的周五来访和基础预算。
- 扩展辑检查使用作者示例核对抽取权重、自动编号、重复键和无效卡池。实际加入项目的每一辑还会经过开包、保存内容往返和跨辑混卡检查。用 `python tests/test_series_art.py` 可在临时项目验证从原画生成纹理的完整命令。

## 用玩家 MCP 试玩卡牌

从独立仓库或发行包 `probably-stolen-player-mcp` 的 README 安装玩家工具，并在模组菜单同时启用“玩家界面读取”和“可能是集换卡牌”。安装《码头来信》或《收藏悬赏》后，也可在同一测试档试玩扩展内容。以下流程用普通玩家能看到的卡包、菜单、图像和库存变化核对结果。[卡牌玩法与供货时间](../ProbablyTradingCards/README.md)

1. 在 MCP 客户端用 `observe` 读取店面。开店后与供货客交谈，用 `hover` 看货品的封口描述，选一包买下，并核对余额与库存变化。
2. 在库存找到刚买的卡包，用 `activate_twice` 拆包。从回执中找到一张新卡，右键选“阅读”查看卡面。若要核对折痕或磨损，用 `look` 看卡边，或用 `detail` 放大已见的截图区域。
3. 打开对应系列的集换册，把单卡拖入同名卡位。核对卡面出现，再取回并确认库存。需要对准具体卡位时，用 `look` 取得当前截图，再按图上的坐标调用 `drag`。

从卡牌仓库的终端读取同一个店面时，将 `$playerRoot` 换成已安装的玩家工具目录。

```powershell
$playerRoot = 'C:\Tools\probably-stolen-player-mcp'
& (Join-Path $playerRoot '.venv/Scripts/python.exe') (Join-Path $playerRoot 'scripts/game_player_ui.py') observe
```

更多界面操作见玩家工具随附的 `docs/player-guide.md`。

## 用正式包跑一轮购买、拆包与读档

从同一工作区构建并安装正式包，启用后重启游戏。准备一个到第 2 天、尚未开店的专用测试档，背包留出两包卡、一本集换册和三张单卡的位置。开店等发行商到访，选常规货单。需要复验星港收藏簿时，可以用第 9 天的档，把下表里的夜话包与集换册换成传奇瘦包与星港收藏簿。[正式包构建](../README.md#构建模组) · [发行商货单](../ProbablyTradingCards/README.md#发行商货单)

用鼠标或上节的玩家工具完成每一行。操作后保持该画面，调用记录命令。截图需要游戏窗口在前台，从终端执行时可加 `--delay 5`，回车后切回游戏。`--player-root` 指向已安装的玩家工具，它的 Python 不在 `.venv` 时用 `--player-python` 指定。

```powershell
$playerRoot = 'C:\Tools\probably-stolen-player-mcp'
$playtestRun = 'artifacts/playtests/night-stories-01'
python tests/playtest.py before-buy --run $playtestRun --player-root $playerRoot --delay 5 --note '测试档与进货前余额'
```

重复该命令，把 `before-buy` 改成下面对应的检查点，并更新 `--note`。可用 `--game-dir '游戏目录'` 覆盖游戏路径。验收独立打包的发布版本时，追加 `--release-dir '本次正式包的目录'`。

| 检查点 | 要做的操作和要核对的结果 |
| --- | --- |
| `before-buy` | 发行商铺货后、首次成交前，记录余额和货单。 |
| `bought` | 买两包夜话和一本夜话集换册，确认实际报价、扣款以及背包中的两包一册。 |
| `moved` | 将其中一包拖到另一个空格再松手，确认仍有两包，且被拖动的包留在新格。 |
| `opened` | 双击刚移动的包，确认只少一包并得到三张单卡。读一张单卡，记下卡名和品相。 |
| `filed` | 将这张卡拖进同名卡位。核对背包少一张，卡套中显示其卡面。检查传奇多页簿时，翻页回来核对卡位和长卡名。 |
| `retrieved` | 将同一张卡取回背包，确认卡位空出，卡名和品相与拆包后一致。 |
| `saved` | 将该卡收回册中，保留另一包原封。记下卡位、原封包位置和测试档，睡觉保存后记录画面，再退出游戏。 |
| `reloaded` | 重启并载入同一测试档。打开册子，确认卡位、品相和背包内的原封包都保留。取出收录的卡，确认能放回背包。 |

每个检查点保存一张 PNG 和对应 JSON，包含工作区提交、安装包指纹、游戏加载审计哈希、游戏进程和你的备注。记录前会比对安装与正式构建的 DLL、清单和贴图，并要求最后一个检查点来自重启后的游戏进程。截图记录只证明捕获到了画面，确认通过需要逐项核对上表。失败时在 `--note` 记下实际结果和停在哪一步，重新开始整轮时换一个输出目录。

## 检查上次启动时加载的模组

`audit` 读取最近一次启动的 `Player.log`，正式包也能使用。游戏退出后仍可查这份日志。CLI 使用 Python 3.9 或更高版本的标准库。

```powershell
python scripts/game_verify.py audit
```

默认检查卡牌前置。若同时安装了两个扩展，可运行 `python scripts/game_verify.py audit probably_trading_cards dock_letters collection_bounty`。日志确认所有指定模组都已运行时，返回的 `status` 为 `passed`，`mods` 中列出各模组版本。默认游戏数据目录为 `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen`。使用其他目录时，在子命令前传 `--data-dir "游戏的持久数据目录"`。

## 安装游戏内验证包

1. 运行 `dotnet build ProbablyTradingCards/ProbablyTradingCards.csproj -c Release -p:NativeValidation=true`。游戏装在其他位置时，加上 `"-p:GameDir=游戏安装目录"`。
2. 退出游戏。将输出目录 `ProbablyTradingCards/dist/NativeValidation/ProbablyTradingCards` 作为游戏的 `Mods/ProbablyTradingCards` 安装。正式包与验证包使用同一模组 ID，游戏的 `Mods` 中只放一份。
3. 在游戏安装目录放置 `dev.txt`，启动游戏，启用模组并按提示重启。进入专用测试档的店铺，松开正在拖动的物品。

## 用 CLI 在真实游戏中验证

先确认 CLI 连上了刚进入的测试档。运行 `status`，应能看到 `state` 为 `ready` 和存档槽编号 `slot`。需要查看当前营业天数、现金和已拥有的卡牌模组物品时，运行 `snapshot`。

```powershell
python scripts/game_verify.py status
python scripts/game_verify.py snapshot
```

将下面的 `3` 换成已确认的测试档槽号。`run` 在游戏主线程执行原生检查。要确认本次确实测到《码头来信》，在命令末尾加 `--require-series dock_letters/dock_demo`。每多检查一辑就再传一次该选项。

```powershell
python scripts/game_verify.py run --slot 3
```

成功时返回 UTF-8 JSON，`status` 为 `passed`，`slot` 为本次槽号，`checks` 为通过的断言数。退出码 `0` 表示命令成功，`1` 表示检查失败，`2` 表示请求受阻或等待超时。默认等待 90 秒，可用 `--timeout 180` 延长。超时后，游戏中已经开始的检查仍可能继续。

排查 CLI 与游戏的通信时，查看游戏数据目录下的 `ModData/probably_trading_cards/agent`。CLI 读取验证包写入的心跳时间，游戏收到请求后会核对会话、槽号和存档标识。修改这部分后，运行 `python tests/test_game_verify.py`，可在临时目录检验过期心跳、错误槽位、回执和超时。[CLI 源码](../scripts/game_verify.py) · [游戏端入口](NativeAgentBridge.cs)

## 原生检查覆盖哪些行为

从游戏里运行同一套检查时，按 F9，在 Special 分类选择“运行原生验证”。检查会重置拖拽状态。在 F8 控制台或 `Player.log` 查找 `NATIVE VALIDATION PASS` 和断言数，或从 `NATIVE VALIDATION FAIL` 后的异常定位失败处。

| 检查对象 | 游戏内断言 |
| --- | --- |
| 商品与估价 | 夜话九张卡的三档品相、原版收购范围、发行商每周额度与买入后溢价清除。拆包爱好者按四种封口线索核价并拒绝超量出售。 |
| 顾客与柜台 | 注册和禁用清理、按编号重建、周四码头供货与跨周补送、周五和周六买家、整盒发售、专题轮换与收购。检查商品能否摆上柜台以及展示柜去重。 |
| 稀有卡预约 | 检查实体卡编号、SSR／UR 报价、逾期再排队、取消和重挂、同款卡误交与重复成交。实际回款和界面用试玩核对。 |
| 拆包与查看 | 对新建和读回的卡包调用原版拖拽处理器，核对开始和取消拖拽都保留包装与内容。另检查买入前看卡、手撕和用刀的品相结果，以及空间不足时保留原包装。 |
| 传奇收藏 | 不同页的实体卡与整盒可保存、读回。检查代表卡的分类、专门买家的收购范围、异画资源和切换画面，分别验证未拥有预览与已拥有查看的发现记录。 |
| 扩展辑 | 遍历启用的内容模组，检查对应作者的各品相资源、打开磨损卡面、发行商货单和补货，拆开保存后读回的包，把单卡收入本辑的册子再读回。 |

验证使用临时库存和顾客管理器，并在模组进度副本上操作。结束时恢复原有引用、营业天数、保存特性列表与模组进度。[原生验证实现](NativeValidation.cs)

## 手动试玩

F9 的“卡牌数据快照”会把当前营业天数、现金和已拥有的卡牌模组物品写入日志。“拆包交互样品”“卡面品相样品”和“星港收藏簿样品”会把样品加入当前库存。调试存取时可先记下快照，再操作样品。

| 修改涉及 | 在游戏里核实 |
| --- | --- |
| 鼠标输入或收藏簿 | 拖动原封包再松手，包装应能换位。双击应正常拆包。把单卡放进相应册页，再翻回取出。 |
| 交易 | 在柜台实际买入和卖出，核对现金变化、货物所有权及供货加价移除。 |
| 存档 | 把物品放入库存或册子，睡觉保存。退出并重启游戏，载入同一个档，核对品相、包装内容与收藏位置。F9 的存取检查在内存中完成。 |

这些步骤也可以用玩家 MCP 在测试档里完成。要核查当前安装包的能力声明，在 F8 控制台运行 `mod-scan probably_trading_cards`。
