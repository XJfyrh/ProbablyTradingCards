using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using ProbablyTradingCards;

if (args.Length is 3 or 4 && args[0] == "--compare")
{
    try
    {
        PublishedIdentities.Check(JsonNode.Parse(File.ReadAllText(args[1])), JsonNode.Parse(File.ReadAllText(args[2])), args.ElementAtOrDefault(3));
        return 0;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine(error.Message);
        return 1;
    }
}
int legacyIndex = Array.IndexOf(args, "--legacy-project");
string legacyProject = null;
if (legacyIndex >= 0 && legacyIndex + 1 < args.Length)
{
    legacyProject = args[legacyIndex + 1];
    args = args.Where((_, i) => i != legacyIndex && i != legacyIndex + 1).ToArray();
}
bool checkCore = args.Contains("--core", StringComparer.Ordinal);
bool writeIdentities = args.Contains("--write-identities", StringComparer.Ordinal);
string[] projects = args.Where(arg => arg is not ("--core" or "--write-identities")).ToArray();
if (projects.Length == 0 || projects.Any(project => project.StartsWith("--", StringComparison.Ordinal)) ||
    ((checkCore || writeIdentities) && projects.Length != 1))
{
    Console.Error.WriteLine("SeriesCheck [--core] [--write-identities] [--legacy-project <published project>] <project> [another project ...]\nSeriesCheck --compare <published snapshot> <current snapshot> [published project]");
    return 2;
}

foreach (string project in projects)
{
    try
    {
        string owner = (string)XDocument.Load(Path.Combine(project, "manifest.xml")).Root?.Element("ID");
        if (string.IsNullOrWhiteSpace(owner)) throw new InvalidDataException("manifest.xml 缺少 ID。");
        IReadOnlyList<CardSeries> series = checkCore
            ? TradingCardsApi.RegisteredSeries.Where(set => set.OwnerId == owner).ToArray()
            : RegisterProjectSeries(project, owner);
        if (series.Count == 0) throw new InvalidDataException($"前置 DLL 中没有属于 {owner} 的卡辑。");
        CheckIdentities(project, owner, series);
        foreach (CardSeries set in series)
            Console.WriteLine($"{set.OwnerId}/{set.Key}（{set.Cards.Count} 张卡，第 {set.ReleaseDay} 天发售）");
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"{project} — {error.GetBaseException().Message}");
        if (error.InnerException != null) Console.Error.WriteLine(error.Message);
        return 1;
    }
}
return 0;

static IReadOnlyList<CardSeries> RegisterProjectSeries(string project, string owner)
{
    string root = Path.Combine(project, "Series");
    var manifests = Directory.GetFiles(root, "series.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
    if (manifests.Length == 0) throw new InvalidDataException("请添加 Series/<key>/series.json。");
    var series = new List<CardSeries>(manifests.Length);
    foreach (string path in manifests)
    {
        string key = Path.GetRelativePath(root, Path.GetDirectoryName(path)!);
        if (key == "." || key.Contains(Path.DirectorySeparatorChar))
            throw new InvalidDataException($"卡辑清单应放在 Series/<key>/series.json：{path}");
        series.Add(TradingCardsApi.RegisterSeries(owner, key, File.ReadAllText(path)));
    }
    return series;
}

void CheckIdentities(string project, string owner, IReadOnlyList<CardSeries> series)
{
    string path = Path.Combine(project, "series-identities.json");
    if (!checkCore && !writeIdentities && !File.Exists(path)) return;

    var snapshot = new
    {
        ownerId = owner,
        series = series.OrderBy(set => set.Key, StringComparer.Ordinal).Select(set => new
        {
            key = set.Key,
            packKind = (int)set.PackKind,
            packSlots = CardGame.GeneratePack(new Random(0), set.PackKind, factory: true).contents.Count,
            packSprite = set.PackSprite,
            albumSprite = set.AlbumSprite,
            albumOrder = set.CardIds,
            cards = set.CardIds.Order().ToDictionary(id => id, id => set.Cards[id].Art)
        })
    };
    var current = JsonSerializer.SerializeToNode(snapshot);
    if (checkCore)
    {
        var legends = current["series"].AsArray().Single(set => (string)set["key"] == "station_legends");
        legends["fatPackSlots"] = StationLegends.SlotCount(PackKind.Fat);
        legends["slimBoxPacks"] = StationLegends.BoxCount(PackKind.Slim);
        legends["fatBoxPacks"] = StationLegends.BoxCount(PackKind.Fat);
    }
    var previous = File.Exists(path)
        ? JsonNode.Parse(File.ReadAllText(path)) ?? throw new InvalidDataException($"{path} 缺少已发布的标识记录。")
        : null;
    if (previous != null) PublishedIdentities.Check(previous, current, legacyProject);
    if (writeIdentities)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, current.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
        Console.WriteLine($"已写入存档标识清单：{path}");
        return;
    }

    if (!JsonNode.DeepEquals(previous, current))
        throw new InvalidDataException($"请用 -p:UpdateSeriesIdentities=true 记录新增内容或升级 {path}。");
}
