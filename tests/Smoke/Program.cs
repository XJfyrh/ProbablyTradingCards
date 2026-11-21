using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProbablyTradingCards;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}
ValidationCleanupChecks.Run(Check);
string Serialize(CardPack pack) => JsonConvert.SerializeObject(pack);
CardPack Copy(CardPack pack) => JsonConvert.DeserializeObject<CardPack>(Serialize(pack));
void Reject(Action action, string message)
{
    bool rejected = false;
    try { action(); }
    catch (InvalidOperationException) { rejected = true; }
    catch (JsonException) { rejected = true; }
    Check(rejected, message);
}

var nightStories = CardGame.Series.ForPack(PackKind.NightStories);
Check(nightStories.CardIds.Count == 9, "Night Stories has nine cards");
var definitions = new HashSet<int>();
var conditions = new HashSet<int>();
var seals = new HashSet<int>();
var resealedStates = new HashSet<bool>();
for (int seed = 0; seed < 2000; seed++)
{
    CardPack pack = CardGame.GeneratePack(new Random(seed), PackKind.NightStories, factory: false);
    CardGame.ValidatePack(pack);
    string sealedData = Serialize(pack);
    Check(Serialize(CardGame.GeneratePack(new Random(seed), PackKind.NightStories, factory: false)) == sealedData, "same seed changes pack contents");
    Check(pack.contents.Count == 3 && pack.tearRolls.Count == 3, "pack must contain three cards and three tear rolls");
    Check(pack.contents.Take(2).All(card => CardGame.Catalogue[card.definition].Rarity == "C"), "first two slots must be common cards");
    CardPack restored = Copy(pack);
    CardGame.ValidatePack(restored);
    Check(Serialize(restored) == sealedData, "pack JSON roundtrip changes hidden contents");
    foreach (bool withCutter in new[] { false, true })
    {
        var opened = CardGame.OpeningContents(restored, withCutter);
        Check(opened.Count == 3, "opening must create exactly three cards");
        Check(opened.Select(card => card.definition).SequenceEqual(pack.contents.Select(card => card.definition)), "opening changes card definitions");
        Check(opened.Select(card => card.condition).SequenceEqual(CardGame.OpeningContents(restored, withCutter).Select(card => card.condition)), "retry changes damage");
        Check(opened.Select(card => card.condition).SequenceEqual(CardGame.OpeningContents(pack, withCutter).Select(card => card.condition)), "reload changes damage");
        Check(opened.Zip(pack.contents).All(pair => pair.First.condition <= pair.Second.condition && pair.First.condition >= Math.Max(0, pair.Second.condition - 1)), "opening changes condition by more than one grade");
        if (withCutter)
            Check(opened.Select(card => card.condition).SequenceEqual(pack.contents.Select(card => card.condition)), "cutter damages cards");
        opened[0].definition = -1;
        Check(Serialize(restored) == sealedData, "opening aliases or mutates sealed data");
    }
    foreach (var card in pack.contents)
    {
        definitions.Add(card.definition);
        conditions.Add(card.condition);
    }
    seals.Add(pack.seal);
    resealedStates.Add(pack.resealed);
}
Check(definitions.SetEquals(nightStories.CardIds), "seeded packs do not exercise the entire Night Stories catalogue");
Check(conditions.Count == CardGame.Conditions.Length && seals.Count == CardGame.Seals.Length && resealedStates.Count == 2, "seeded packs omit a condition, seal or resealed state");

CardPack boundary = CardGame.GeneratePack(new Random(9), PackKind.NightStories, factory: false);
boundary.contents.ForEach(card => card.condition = 2);
boundary.tearRolls = new List<int> { 0, 24, 25 };
Check(CardGame.OpeningContents(boundary, false).Select(card => card.condition).SequenceEqual(new[] { 1, 1, 2 }), "25 percent tear damage boundary");
Check(CardGame.OpeningContents(boundary, true).All(card => card.condition == 2), "cutter must preserve every condition");
boundary.contents.ForEach(card => card.condition = 0);
Check(CardGame.OpeningContents(boundary, false).All(card => card.condition == 0), "damage falls below worst condition");

var invalidCases = new (string name, Action<CardPack> mutate)[]
{
    ("missing contents", pack => pack.contents = null),
    ("missing card", pack => pack.contents.RemoveAt(0)),
    ("extra card", pack => pack.contents.Add(new OwnedCard { definition = 0, condition = 2 })),
    ("null card", pack => pack.contents[0] = null),
    ("negative definition", pack => pack.contents[0].definition = -1),
    ("wrong series", pack => pack.contents[0].definition = StationLegends.EleanorRev),
    ("negative condition", pack => pack.contents[0].condition = -1),
    ("unknown condition", pack => pack.contents[0].condition = CardGame.Conditions.Length),
    ("negative seal", pack => pack.seal = -1),
    ("unknown seal", pack => pack.seal = CardGame.Seals.Length),
    ("missing tear rolls", pack => pack.tearRolls = null),
    ("empty tear rolls", pack => pack.tearRolls.Clear()),
    ("short tear rolls", pack => pack.tearRolls.RemoveAt(0)),
    ("extra tear roll", pack => pack.tearRolls.Add(1)),
    ("negative tear roll", pack => pack.tearRolls[0] = -1),
    ("out-of-range tear roll", pack => pack.tearRolls[0] = 100)
};
Reject(() => CardGame.ValidatePack(null), "null pack accepted");
foreach (var invalid in invalidCases)
{
    CardPack pack = Copy(boundary);
    invalid.mutate(pack);
    Reject(() => CardGame.ValidatePack(pack), invalid.name + " accepted by validation");
    Reject(() => CardGame.OpeningContents(pack, false), invalid.name + " accepted by opening");
}
foreach (string field in new[] { "resealed", "seal", "contents", "tearRolls" })
{
    JObject json = JObject.Parse(Serialize(boundary));
    json.Remove(field);
    Reject(() => JsonConvert.DeserializeObject<CardPack>(json.ToString()), "missing pack field silently defaulted: " + field);
}
foreach (string field in new[] { "definition", "condition" })
{
    JObject json = JObject.Parse(Serialize(boundary));
    ((JObject)json["contents"][0]).Remove(field);
    Reject(() => JsonConvert.DeserializeObject<CardPack>(json.ToString()), "missing card field silently defaulted: " + field);
}
var factoryThirdSlot = new HashSet<int>();
for (int seed = 0; seed < 2000; seed++)
{
    CardPack genuine = CardGame.GeneratePack(new Random(seed), PackKind.NightStories, factory: true);
    CardGame.ValidatePack(genuine);
    factoryThirdSlot.Add(genuine.contents[2].definition);
    Check(!genuine.resealed, "distributor generated a resealed pack");
    Check(genuine.seal <= 2, "genuine pack has a resealed-only clue");
    Check(Serialize(Copy(genuine)) == Serialize(genuine), "genuine pack changes after save roundtrip");
}
Check(factoryThirdSlot.SetEquals(nightStories.CardIds),
    "the factory pack's third slot must draw from all nine Night Stories cards");
foreach (var (factory, resealed, conditionCounts, rarityCounts) in new[]
{
    (true, false, new[] { 1, 9, 90 }, new[] { 60, 34, 6 }),
    (false, false, new[] { 5, 25, 70 }, new[] { 60, 34, 6 }),
    (false, true, new[] { 25, 45, 30 }, new[] { 90, 9, 1 })
})
{
    var actualConditions = new int[3];
    var actualRarities = new int[3];
    for (int roll = 0; roll < 100; roll++)
    {
        var random = factory
            ? new ScriptedRandom(roll, roll, roll, roll, 0, 0, 0, 0)
            : new ScriptedRandom(resealed ? 0 : 99, roll, roll, roll, roll, 0, 0, 0, 0);
        CardPack pack = CardGame.GeneratePack(random, PackKind.NightStories, factory);
        Check(random.Remaining == 0, "all scripted percent rolls must be consumed");
        Check(pack.resealed == resealed, "source must select the expected authenticity");
        Check(pack.contents.Take(2).All(card => CardGame.Catalogue[card.definition].Rarity == "C"), "first two slots must remain common");
        int expectedCondition = roll < conditionCounts[0] ? 0 : roll < conditionCounts[0] + conditionCounts[1] ? 1 : 2;
        Check(pack.contents.All(card => card.condition == expectedCondition), "source condition boundary");
        string expectedRarity = roll < rarityCounts[2] ? "SR" : roll < rarityCounts[2] + rarityCounts[1] ? "R" : "C";
        Check(CardGame.Catalogue[pack.contents[2].definition].Rarity == expectedRarity, "third-slot rarity boundary");
        actualConditions[pack.contents[0].condition]++;
        actualRarities[Array.IndexOf(new[] { "C", "R", "SR" }, CardGame.Catalogue[pack.contents[2].definition].Rarity)]++;
    }
    Check(actualConditions.SequenceEqual(conditionCounts), "exact source condition percentages");
    Check(actualRarities.SequenceEqual(rarityCounts), "exact source rarity percentages");
}
for (int roll = 0; roll < 100; roll++)
{
    CardPack pack = CardGame.GeneratePack(new ScriptedRandom(roll, 99, 99, 99, 99, 99, 99, 99, 99), PackKind.NightStories, factory: false);
    Check(pack.resealed == (roll < 25), "scatter seller resealed boundary");
}
Check(CardGame.Series.ForPack(PackKind.NightStories).CardIds.Select(id => CardGame.Catalogue[id].Value)
    .SequenceEqual(new[] { 4, 6, 8, 10, 40, 60, 80, 240, 360 }), "card base prices");
Check(CardGame.ConditionPercentages.SequenceEqual(new[] { 30, 75, 100 }), "condition price percentages");
Check(CardGame.PackValue(PackKind.NightStories) == 40, "pack base price");

foreach (var (day, rolls) in new[] { (1, 2), (19, 2), (20, 3), (49, 3), (50, 4), (100, 4) })
{
    for (int mask = 0; mask < 1 << rolls; mask++)
    {
        int called = 0;
        int expected = 2;
        for (int index = 0; index < rolls; index++)
            if ((mask & (1 << index)) != 0) expected++;
        int count = CardCustomerRules.SellerPackCount(day, chance =>
        {
            Check(chance == 50, "extra pack chance changed");
            return (mask & (1 << called++)) != 0;
        });
        Check(called == rolls, "extra pack rolls are not independent or have the wrong day threshold");
        Check(count == expected && count >= 2 && count <= 6, "seller quantity outside the agreed progression");
    }
    Check(CardCustomerRules.CollectorBaseBudget(day, 0) == 200 + 3 * day, "collector minimum base budget");
    Check(CardCustomerRules.CollectorBaseBudget(day, 100) == 300 + 3 * day, "collector maximum base budget");
}
Check(!CardCustomerRules.IsDistributorDay(0), "distributor scheduled before the game begins");
Check(!CardCustomerRules.IsFriday(0), "night collector scheduled before the game begins");
Check(!CardCustomerRules.IsDockSellerDue(0), "dock seller scheduled before the game begins");
Check(CardCustomerRules.WeeklyNightPacks == 6 && CardCustomerRules.WeeklySlimPacks == 3 &&
    CardCustomerRules.WeeklyFatPacks == 2 && CardCustomerRules.WeeklyDockPacks == 3, "weekly supply quantities");
Check(StationLegends.FatPackDay == 16 && StationLegends.SlimBoxDay == 23 && StationLegends.FatBoxDay == 30,
    "fat packs and the two sealed boxes release on successive Tuesdays");
for (int day = 1; day <= 70; day++)
{
    Check(CardCustomerRules.IsDistributorDay(day) == new[] { 2, 9, 16, 23, 30, 37, 44, 51, 58, 65 }.Contains(day),
        "distributor schedule must start on the first Tuesday and repeat weekly");
    Check(CardCustomerRules.IsFriday(day) == new[] { 5, 12, 19, 26, 33, 40, 47, 54, 61, 68 }.Contains(day),
        "night collector schedule must start on the first Friday and repeat weekly");
    int weekday = day % 7;
    Check(CardCustomerRules.IsDockSellerDue(day) == (weekday == 0 || weekday >= 4),
        "pending dock seller is due Thursday through Sunday");
    Check(CardCustomerRules.SupplyWeek(day) == (int)Math.Floor((day - 1) / 7.0),
        "weekly quotas reset on Monday");
}
Check(CardCustomerRules.SellerAlbumChance == 10, "seller album chance");

var legends = CardGame.Catalogue.Values.Where(card => card.IsLegends && !card.IsAlternate).ToArray();
Check(legends.Length == 40, "Legends has forty base cards");
foreach (var (rarity, count) in new[] { ("C", 7), ("R", 13), ("SR", 7), ("SSR", 8), ("UR", 5) })
    Check(legends.Count(card => card.Rarity == rarity) == count, $"Legends has {count} base {rarity} cards");
Check(legends.Count(card => card.Subject == CardSubject.Character) == 24, "Legends character catalogue");
var alternates = CardGame.Catalogue.Values.Where(card => card.IsLegends && card.IsAlternate).ToArray();
Check(alternates.Length == 11, "Legends has eleven alternate cards");
Check(alternates.Where(card => card.Rarity == "UR").Select(card => card.Art).SequenceEqual(new[] { "joe-network" }),
    "Joe is the only active UR alternate");
Check(CardGame.Catalogue.Values.Where(card => card.IsAlternate).All(card =>
    CardGame.Catalogue[card.AlternateOf.Value].Rarity == card.Rarity), "alternate inherits rarity");
Check(CardGame.PackValue(PackKind.Slim) == 120 && CardGame.PackValue(PackKind.Fat) == 480, "Legends pack values");
Check(StationLegends.BoxValue(PackKind.Slim) == 1440 && StationLegends.BoxValue(PackKind.Fat) == 2880, "Legends box values");

Check(Enumerable.Range(1, 40).Where(CardScalperRules.Visits).SequenceEqual(new[] { 12, 19, 26, 33, 40 }),
    "scalper visits first Friday after Legends and every Friday thereafter");
Check(CardScalperRules.PackKinds(8).Length == 0 &&
    CardScalperRules.PackKinds(12).SequenceEqual(new[] { PackKind.Slim }) &&
    CardScalperRules.PackKinds(19).SequenceEqual(new[] { PackKind.Slim, PackKind.Fat }),
    "scalper offers only released Legends loose packs");
Check(CardScalperRules.PackPrice(PackKind.Slim) == 270 && CardScalperRules.PackPrice(PackKind.Fat) == 1080,
    "scalper pack quotes");
Check(CardScalperRules.BoxPrice(PackKind.Slim) == 3240 && CardScalperRules.BoxPrice(PackKind.Fat) == 6480,
    "scalper box quotes");
Check(CardScalperRules.RollBox(22, new ScavengingRandom()) == null, "scalper rolls no box before release");
for (int roll = 0; roll < 100; roll++)
{
    var firstBox = CardScalperRules.RollBox(23, new ScavengingRandom((100, roll)));
    PackKind? expectedEarly = roll < 10 ? PackKind.Slim : null;
    Check(firstBox == expectedEarly, "scalper's early box roll gives only the slim box");
    for (int boxType = 0; boxType < 2; boxType++)
    {
        var random = roll < 10 ? new ScavengingRandom((100, roll), (2, boxType)) : new ScavengingRandom((100, roll));
        PackKind? expected = roll < 10 ? (boxType == 0 ? PackKind.Slim : PackKind.Fat) : null;
        Check(CardScalperRules.RollBox(30, random) == expected, "scalper's late box roll splits ten percent between both boxes");
    }
}
Check(CardScalperRules.SingleRarity(5) == null &&
    new[] { 12, 19, 26, 33, 40 }.Select(CardScalperRules.SingleRarity).SequenceEqual(new[] { "SR", "SSR", "SR", "UR", "SR" }),
    "scalper single rotation starts with Legends");
foreach (var rarity in new[] { "SR", "SSR", "UR" })
{
    var candidates = legends.Where(card => card.Rarity == rarity).ToArray();
    var unowned = candidates.Last();
    var owned = candidates.Where(card => card != unowned).Select(card => card.Id).ToHashSet();
    var single = CardScalperRules.SelectSingle(rarity, owned, new Random(4));
    Check(single == unowned && CardScalperRules.SinglePrice(single) == single.Value * 3,
        "scalper offers the missing Legends base card at triple its base price");
    owned.Add(unowned.Id);
    Check(candidates.Contains(CardScalperRules.SelectSingle(rarity, owned, new Random(4))),
        "scalper can offer a mint replacement when that rarity is complete");
}
var weeklyPack = CardGame.GeneratePack(CardScalperRules.StockRandom("test-save", 3, (int)PackKind.Fat), PackKind.Fat, true);
var weeklyPackAfterReload = CardGame.GeneratePack(CardScalperRules.StockRandom("test-save", 3, (int)PackKind.Fat), PackKind.Fat, true);
Check(!weeklyPack.resealed && Serialize(weeklyPack) == Serialize(weeklyPackAfterReload),
    "weekly scalper pack is factory sealed and stable after reopening the save");

foreach (var (day, expected) in new[]
{
    (8, new[] { 100, 0, 0 }), (9, new[] { 50, 50, 0 }), (15, new[] { 50, 50, 0 }), (16, new[] { 50, 40, 10 })
})
{
    var counts = new int[3];
    for (int roll = 0; roll < 100; roll++) counts[(int)StationLegends.SellerPackKind(day, roll)]++;
    Check(counts.SequenceEqual(expected), "second series seller mix");
}

foreach (var (kind, cardsPerPack, packsPerBox) in new[] { (PackKind.Slim, 3, 12), (PackKind.Fat, 9, 6) })
{
    for (int seed = 0; seed < 500; seed++)
    {
        var pack = CardGame.GeneratePack(new Random(seed), kind, factory: true);
        CardGame.ValidatePack(pack);
        Check(!pack.resealed && pack.kind == kind && pack.contents.Count == cardsPerPack, "factory pack kind and capacity");
        var rarities = pack.contents.Select(card => CardGame.Catalogue[card.definition].Rarity).ToArray();
        Check(rarities.Take(kind == PackKind.Slim ? 1 : 4).All(rarity => rarity == "C"), "factory pack common slots");
        if (kind == PackKind.Fat) Check(rarities[4] == "R" && rarities[5] == "R", "factory fat pack rare slots");
        Check(rarities.Last() != "C" && (kind == PackKind.Slim || rarities.Last() != "R"), "factory pack guarantees");
        Check(Serialize(Copy(pack)) == Serialize(pack), "new pack JSON round trip");
        var box = StationLegends.GenerateBox(new Random(seed), kind);
        Check(box.packs.Count == packsPerBox, "factory box capacity");
        StationLegends.ValidateBox(box, kind);
        var restored = JsonConvert.DeserializeObject<CardBox>(JsonConvert.SerializeObject(box));
        StationLegends.ValidateBox(restored, kind);
        Check(box.packs.Select(Serialize).SequenceEqual(restored.packs.Select(Serialize)), "nested box packs retain exact data and order");
    }

    var hits = new[] { StationLegends.EleanorRev, StationLegends.SerpicoRev, StationLegends.Joe };
    foreach (var (roll, pick, resealed, expected) in new[]
    {
        (19, 0, false, StationLegends.EleanorRev),
        (19, 200000, false, StationLegends.SerpicoRev),
        (19, 400000, false, StationLegends.Joe),
        (20, 0, false, -1),
        (0, 0, true, -1)
    })
    {
        var pack = CardGame.GeneratePack(new Random(1), kind, factory: true);
        pack.resealed = resealed;
        pack.alternateRoll = roll;
        pack.alternatePick = pick;
        StationLegends.ResolveAlternate(pack, hits);
        Check(pack.resolvedAlternate == expected, "alternate chance or weighted pick boundary");
        var restored = Copy(pack);
        StationLegends.ResolveAlternate(restored, new[] { StationLegends.Waste }, luck: 100);
        Check(restored.resolvedAlternate == expected, "a failed opening cannot reroll alternate on a later day");
        var contents = CardGame.OpeningContents(restored, true);
        Check(contents.Count == pack.contents.Count && contents.Last().definition ==
            (expected >= 0 ? expected : pack.contents.Last().definition), "alternate replaces the correct slot");
        Check(contents.Last().condition == pack.contents.Last().condition, "alternate inherits slot condition");
    }

    var unopened = CardGame.GeneratePack(new Random(2), kind, factory: true);
    foreach (var (luck, threshold) in new[]
    {
        (int.MinValue, 10), (-15, 17), (0, 20), (2, 20), (3, 21), (5, 21),
        (15, 23), (75, 35), (95, 39), (int.MaxValue, 40)
    })
    {
        unopened.alternateRoll = threshold - 1;
        var hit = Copy(unopened);
        StationLegends.ResolveAlternate(hit, new[] { StationLegends.Joe }, luck);
        Check(hit.resolvedAlternate == StationLegends.Joe, "one roll below the luck threshold hits");

        unopened.alternateRoll = threshold;
        var miss = Copy(unopened);
        StationLegends.ResolveAlternate(miss, new[] { StationLegends.Joe }, luck);
        Check(miss.resolvedAlternate == -1, "roll equal to the luck threshold misses");
    }

    unopened.alternateRoll = 0;
    var noCandidate = Copy(unopened);
    StationLegends.ResolveAlternate(noCandidate, Array.Empty<int>(), luck: 100);
    Check(noCandidate.resolvedAlternate == -1, "luck cannot open ineligible alternates");
    unopened.resealed = true;
    StationLegends.ResolveAlternate(unopened, hits, luck: 100);
    Check(unopened.resolvedAlternate == -1, "luck cannot add alternates to resealed packs");
}
Check(!StationLegends.InWindow(8, 6, 14) && StationLegends.InWindow(9, 6, 14) &&
    StationLegends.InWindow(22, 6, 14) && !StationLegends.InWindow(23, 6, 14), "window starts no earlier than release");
Check(StationLegends.InWindow(36, 23, 14) && !StationLegends.InWindow(37, 23, 14) &&
    !StationLegends.InWindow(23, -1, 14), "story window includes first day and excludes end");
// Use the author's sample verbatim, independently of which series this checkout ships.
using var exampleReader = new StreamReader(typeof(CardGame).Assembly.GetManifestResourceStream("examples/dock_demo.json"));
string exampleJson = exampleReader.ReadToEnd();
var sample = JObject.Parse(exampleJson);
CardRegistry MakeRegistry(IEnumerable<(string key, string json)> inputs)
{
    var registry = new CardRegistry();
    foreach (var (key, json) in inputs) registry.Register("dock_letters", key, json);
    return registry;
}

var exampleRegistry = MakeRegistry(new[] { ("dock_demo", exampleJson) });
var demo = exampleRegistry.All.Single();
int common = demo.CardIds[0], rare = demo.CardIds[1];
Check(demo.IsExpansion && demo.Cards[rare].Number == 2 && demo.PackSprite == "pack" && demo.AlbumSprite == "legends_album",
    "sample uses its own numbering and stock packaging sprites");
for (int roll = 0; roll < 4; roll++)
{
    var pack = demo.GenerateStandardPack(new WeightRollRandom(4, roll), true);
    Check(pack.kind == demo.PackKind && pack.contents.Select(card => card.definition)
        .SequenceEqual(new[] { common, roll < 3 ? common : rare }), "sample's 3:1 slot boundary");
    Check(Serialize(Copy(pack)) == Serialize(pack), "sample pack JSON round trip");
}
var reordered = (JObject)sample.DeepClone();
reordered["cards"] = new JArray(reordered["cards"].Reverse().ToArray());
var other = (JObject)sample.DeepClone();
other["key"] = "dock_neighbor";
var combined = MakeRegistry(new[] { ("dock_neighbor", other.ToString()), ("dock_demo", reordered.ToString()) });
Check(combined.ForPack(demo.PackKind).CardIds.SequenceEqual(new[] { rare, common }) &&
    combined.Cards[common].Series.Key == "dock_demo" && combined.Cards.Count == 4,
    "adding a series and reordering cards keeps generated IDs and isolates art keys");

void RejectExample(Action<JObject> change)
{
    var invalid = (JObject)sample.DeepClone();
    change(invalid);
    Reject(() => MakeRegistry(new[] { ("dock_demo", invalid.ToString()) }), "invalid series accepted: " + invalid);
}
RejectExample(data => data["key"] = "other");
RejectExample(data => data["cards"][1]["art"] = "courier");
RejectExample(data => data["cards"][1]["art"] = "../ticket");
RejectExample(data => data["packKind"] = 2);
RejectExample(data => data["cards"][0]["id"] = data["cards"][1]["id"] = 100);
RejectExample(data => data["slots"][1]["R"] = 0);
RejectExample(data => data["slots"][1]["UR"] = 1);
RejectExample(data => ((JObject)data["slots"][1]).Remove("R"));
RejectExample(data => data["resealedSlots"] = new JArray());
RejectExample(data => data["releaseDays"] = 3);
other["cards"][0]["id"] = common;
Reject(() => MakeRegistry(new[] { ("dock_demo", exampleJson), ("dock_neighbor", other.ToString()) }),
    "duplicate card ID across two series");
other["packKind"] = (int)demo.PackKind;
Reject(() => MakeRegistry(new[] { ("dock_demo", exampleJson), ("dock_neighbor", other.ToString()) }),
    "duplicate pack kind across two series");

// Register the independent author's content through the same entry point as the built-ins.
var live = TradingCardsApi.RegisterSeries("dock_letters", "dock_demo", exampleJson);
var neighbor = TradingCardsApi.RegisterSeries("other_author", "dock_demo", exampleJson);
Check(live.PackKind != neighbor.PackKind && !live.CardIds.Intersect(neighbor.CardIds).Any(),
    "same series and art keys from different owners must coexist");
Check(live.PackSpriteOwnerId == CardRegistry.CoreOwnerId && live.AlbumSpriteOwnerId == CardRegistry.CoreOwnerId &&
    live.Cards[live.CardIds[0]].Subject == CardSubject.Character, "shared packaging and character subject");
int countBeforeFailure = CardGame.Catalogue.Count;
int packCountBeforeFailure = TradingCardsApi.RegisteredSeries.Count;
var colliding = (JObject)sample.DeepClone();
colliding["cards"][1]["id"] = live.CardIds[1];
Reject(() => TradingCardsApi.RegisterSeries("conflict", "dock_demo", colliding.ToString()), "second card collided");
Reject(() => TradingCardsApi.RegisterSeries("dock_letters", "dock_demo", exampleJson), "same author and key registered twice");
Check(CardGame.Catalogue.Count == countBeforeFailure && TradingCardsApi.RegisteredSeries.Count == packCountBeforeFailure &&
    !TradingCardsApi.TryGetSeries("conflict", "dock_demo", out _), "invalid add-on contaminated the running registry");
var namesake = (JObject)sample.DeepClone();
namesake["key"] = "station_legends";
var named = TradingCardsApi.RegisterSeries("other_author", "station_legends", namesake.ToString());
Check(named.IsExpansion && !named.Cards.Values.Any(card => card.IsLegends), "external namesake inherited built-in rules");
Check(TradingCardsApi.UnregisterSeries("other_author", "station_legends"), "namesake removal");

Check(ScavengedCards.Roll(0, new ScavengingRandom()) == null, "scavenging before Night Stories release");
Check(ScavengedCards.Roll(9, new ScavengingRandom((10_000, 1_200))) == null, "scavenging card chance ends at 12%");
foreach (var (day, edges) in new[]
{
    (8, new[] { (0, "C"), (8_049, "C"), (8_050, "R"), (9_749, "R"), (9_750, "SR"), (9_999, "SR") }),
    (9, new[] { (0, "C"), (7_999, "C"), (8_000, "R"), (9_699, "R"), (9_700, "SR"),
        (9_949, "SR"), (9_950, "SSR"), (9_994, "SSR"), (9_995, "UR"), (9_999, "UR") })
})
{
    foreach (var (rarityRoll, rarity) in edges)
    {
        var expected = CardGame.Catalogue.Values.Where(card => !card.IsAlternate && card.Rarity == rarity &&
            (card.Series == nightStories || (day == 9 && card.IsLegends))).Select(card => card.Id).ToHashSet();
        var selected = new HashSet<int>();
        for (int index = 0; index < expected.Count; index++)
        {
            var card = ScavengedCards.Roll(day, new ScavengingRandom((10_000, 1_199), (10_000, rarityRoll),
                (expected.Count, index), (10_000, 0)));
            Check(card != null && card.condition == 0 && selected.Add(card.definition), "scavenging pool index or drop boundary");
        }
        Check(expected.Count > 0 && expected.SetEquals(selected), $"day {day}: scavenging pool for {rarity}");
    }
}
int earlyCommons = nightStories.Cards.Values.Count(card => card.Rarity == "C");
foreach (var (wearRoll, condition) in new[] { (0, 0), (8_499, 0), (8_500, 1), (9_949, 1), (9_950, 2), (9_999, 2) })
{
    var card = ScavengedCards.Roll(1, new ScavengingRandom((10_000, 0), (10_000, 0), (earlyCommons, 0), (10_000, wearRoll)));
    Check(card.condition == condition, "scavenging card wear boundary");
}

foreach (var series in CardGame.Series.All.Where(series => series.IsExpansion))
{
    for (int seed = 0; seed < 32; seed++)
    {
        var pack = CardGame.GeneratePack(new Random(seed), series.PackKind, factory: seed % 2 == 0);
        var saved = Copy(pack);
        var opened = CardGame.OpeningContents(saved, withCutter: true);
        Check(opened.Select(card => (card.definition, card.condition)).SequenceEqual(
            pack.contents.Select(card => (card.definition, card.condition))) &&
            opened.All(card => CardGame.Catalogue[card.definition].Series == series), series.Key + ": pack opens into its own cards");
        saved.contents[0].definition = CardGame.Series.ForPack(PackKind.NightStories).CardIds[0];
        Reject(() => CardGame.ValidatePack(saved), series.Key + ": mixed-series pack accepted");
        var legendsPack = CardGame.GeneratePack(new Random(seed), PackKind.Slim, true);
        legendsPack.resolvedAlternate = opened[0].definition;
        Reject(() => CardGame.ValidatePack(legendsPack), series.Key + ": expansion card accepted as a Legends alternate");
    }
}
Check(TradingCardsApi.UnregisterSeries("other_author", "dock_demo") &&
    !TradingCardsApi.UnregisterSeries("other_author", "dock_demo") &&
    !neighbor.CardIds.Any(CardGame.Catalogue.ContainsKey) && TradingCardsApi.TryGetSeries("dock_letters", "dock_demo", out _),
    "disabling one add-on must remove only its content");
Check(TradingCardsApi.UnregisterSeries("dock_letters", "dock_demo") &&
    TradingCardsApi.RegisterSeries("dock_letters", "dock_demo", exampleJson).CardIds.SequenceEqual(live.CardIds),
    "re-enabling content must restore its identifiers");
TradingCardsApi.UnregisterSeries("dock_letters", "dock_demo");
Console.WriteLine($"PASS {checks} checks. Registered series: {string.Join(", ", CardGame.Series.All.Select(series => series.Key))}.");

sealed class WeightRollRandom(int scale, int roll) : Random
{
    public override int Next(int maxValue) => maxValue == scale ? roll : 0;
}

sealed class ScavengingRandom(params (int Scale, int Roll)[] sequence) : Random
{
    private readonly Queue<(int Scale, int Roll)> rolls = new(sequence);

    public override int Next(int maxValue)
    {
        var (scale, roll) = rolls.Dequeue();
        if (scale != maxValue) throw new Exception($"Expected scavenging roll below {scale}, got {maxValue}.");
        return roll;
    }
}

sealed class ScriptedRandom : Random
{
    private readonly Queue<int> percentRolls;
    public int Remaining => percentRolls.Count;

    public ScriptedRandom(params int[] rolls) => percentRolls = new Queue<int>(rolls);
    public override int Next(int maxValue) => maxValue == 100 ? percentRolls.Dequeue() : 0;
    public override int Next(int minValue, int maxValue) => minValue;
}
