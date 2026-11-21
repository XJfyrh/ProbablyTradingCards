using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProbablyTradingCards;

bool scanWorkshop = args.Length == 3 && args[0] == "--scan-workshop";
if (args.Length != 2 && !scanWorkshop)
    throw new ArgumentException("Expected <repository root> <game directory> or --scan-workshop <package> <game directory>.");
string managed = Path.Combine(Path.GetFullPath(args[^1]), "Probably Stolen_Data", "Managed");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string candidate = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
};
RegisterGameAssemblies(managed);
if (scanWorkshop)
{
    CheckPackage(Path.GetFullPath(args[1]), "ProbablyTradingCards", new Dictionary<string, string>());
    Console.WriteLine("PASS: ProbablyTradingCards package under Workshop scanner rules.");
}
else
{
    Run(Path.GetFullPath(args[0]));
}

static void Require(bool valid, string description)
{
    if (!valid) throw new InvalidOperationException(description);
}

static void RegisterGameAssemblies(string managed)
{
    var gameAssemblies = new HashSet<string>(System.IO.Directory.EnumerateFiles(managed, "*.dll", SearchOption.AllDirectories)
        .Select(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);
    typeof(ModLoader).GetField("gameAssemblyNames", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, gameAssemblies);
}

static void Run(string root)
{
    string content = Path.Combine(root, "Examples", "DockLetters");
    string dockPackage = Path.Combine(content, "dist", "DockLetters");
    string bountyPackage = Path.Combine(root, "Examples", "CollectionBounty", "dist", "CollectionBounty");
    string corePackage = Path.Combine(root, "ProbablyTradingCards", "dist", "ProbablyTradingCards");
    var available = new Dictionary<string, string>();
    CheckPackage(corePackage, "ProbablyTradingCards", available);
    var dockManifest = CheckPackage(dockPackage, "DockLetters", available);
    var bountyManifest = CheckPackage(bountyPackage, "CollectionBounty", available);
    IMod dockProvider = new DockLetters.ModEntry();
    IMod bounty = new CollectionBounty.ModEntry();
    dockProvider.Init(dockManifest);
    bounty.Init(bountyManifest);

    // A mechanics mod can run with just the base game content, before any external set exists.
    bounty.OnEnable();
    Require(!TradingCardsApi.TryGetSeries(dockManifest.ID, "dock_demo", out _), "content leaked into the base DLL");
    try
    {
        using var bad = TradingCardsApi.RegisterEmbeddedSeries("broken_pack", typeof(BountyChecks).Assembly);
        throw new Exception("Malformed content package loaded");
    }
    catch (InvalidOperationException error)
    {
        Require(error.Message.Contains("Series/invalid/series.json") && !TradingCardsApi.TryGetSeries("broken_pack", "dock_demo", out _),
            "partially registered a package with a bad second series");
    }
    dockProvider.OnEnable();
    dockProvider.OnEnable();
    Require(TradingCardsApi.TryGetSeries(dockManifest.ID, "dock_demo", out var dock), "external DLL did not register");
    var manifest = JObject.Parse(File.ReadAllText(Path.Combine(content, "Series", "dock_demo", "series.json")));
    manifest["customPackArt"] = manifest["customAlbumArt"] = true;
    var second = TradingCardsApi.RegisterSeries("other_author", dock.Key, manifest.ToString());
    NativeDirectoryChecks.Run(dock);
    AlbumSaveChecks.Run(dock);

    foreach (var card in dock.Cards.Values)
    {
        string sprite = "card_set_" + dock.Key + "_" + card.Art;
        Require(File.Exists(Path.Combine(dockPackage, "Textures", "Paper", sprite + ".png")), "Missing face: " + sprite);
        foreach (string suffix in new[] { "", "--wear-0", "--wear-1" })
            Require(File.Exists(Path.Combine(dockPackage, "Textures", "Items", sprite + suffix + ".png")), "Missing icon: " + sprite + suffix);
    }
    var pack = CardGame.GeneratePack(new Random(28), dock.PackKind, factory: true);
    var restored = JsonConvert.DeserializeObject<CardPack>(JsonConvert.SerializeObject(pack));
    var opened = CardGame.OpeningContents(restored, withCutter: true);
    Require(opened.Count == 2 && opened.All(row => dock.Cards.ContainsKey(row.definition)), "pack lost its owner's cards");
    var courier = dock.Cards[opened[0].definition];
    var ticket = dock.Cards.Values.Single(card => card.Subject == CardSubject.Item);
    var guest = TradingCardsApi.RegisteredSeries.SelectMany(series => series.Cards.Values)
        .First(card => card.IsLegends && card.IsLuxury && card.Subject == CardSubject.Character);
    BountyChecks.Run(Single(courier, 0, 101), Single(courier, 2, 102), Single(guest, 1, 103), Single(ticket, 2, 104));

    TradingCardsApi.UnregisterSeries(second.OwnerId, second.Key);
    NativeDirectoryChecks.CheckRemoved(second);
    Require(TradingCardsApi.TryGetSeries(dockManifest.ID, dock.Key, out _), "disabling a namesake removed its neighbor");
    dockProvider.OnDisable();
    NativeDirectoryChecks.CheckRemoved(dock);
    bounty.OnDisable();
    Require(!StoreClientListDict.storeClientDict.ContainsKey(bountyManifest.ID + ".editor"), "buyer still registered");
    dockProvider.OnEnable();
    Require(TradingCardsApi.TryGetSeries(dockManifest.ID, dock.Key, out var again) && again.CardIds.SequenceEqual(dock.CardIds),
        "re-enabling the add-on changed card IDs");
    dockProvider.OnDisable();
    Console.WriteLine("PASS: add-on packages, native item registration, owner-scoped art, saved packs, character sales, reward and disable/re-enable.");
}

static GameItem Single(CardDefinition card, int condition, int uniqueId)
{
    // Headless equivalent of the identity tags on a physical card returned by the native factory.
    var item = (GameItem)RuntimeHelpers.GetUninitializedObject(typeof(GameItemElement));
    item.identifier = NativeItems.CardId(card.Id, condition);
    item.uniqueId = uniqueId;
    item.state = item.modifiedState = new TagSystem();
    item.state.EnableTag(NativeItems.CardTag);
    item.state.GetTag(NativeItems.DefinitionTag).Enable().SetInt(card.Id);
    item.state.GetTag(NativeItems.ConditionTag).Enable().SetInt(condition);
    if (card.IsLuxury) item.state.EnableTag(TypeHelper.LUXURY_ITEM);
    return item;
}

static ModManifest CheckPackage(string folder, string name, Dictionary<string, string> available)
{
    var xml = XDocument.Load(Path.Combine(folder, "manifest.xml")).Root!;
    var manifest = new ModManifest
    {
        ID = (string)xml.Element("ID"), Name = (string)xml.Element("Name"),
        Prerequisites = xml.Element("Prerequisites")!.Elements("Mod").Select(entry => entry.Value).ToList(),
        Assemblies = xml.Element("Assemblies")!.Elements("Assembly").Select(entry => entry.Value).ToList()
    };
    Require(manifest.Assemblies.SequenceEqual(new[] { name + ".dll" }), "manifest DLL: " + name);
    Require(name == "ProbablyTradingCards" || manifest.Prerequisites.SequenceEqual(new[] { ModEntry.ModId }), "base dependency: " + name);
    var dll = System.IO.Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories).Single();
    Require(Path.GetFileName(dll) == name + ".dll", "unexpected packaged DLL: " + name);
    var allowed = manifest.Prerequisites.Select(id => available[id]).Append(name);
    var scan = ModScanner.Scan(new[] { dll }, allowed, folder, false);
    Require(!scan.Refused && scan.Capabilities.Count == 0 && scan.Pending.Count == 0,
        name + " scanner: " + scan.Error + " " + scan.FormatFindings(10));
    available.Add(manifest.ID, name);
    return manifest;
}
