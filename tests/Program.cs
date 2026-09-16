using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PhilosophyTester.Models;
using PhilosophyTester.Services;

var root = Directory.GetCurrentDirectory();
var dataRoot = Path.Combine(root, "wwwroot", "data");
var manifest = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(dataRoot, "tests", "index.json")), AppJsonContext.Default.TestManifest)!;
var tests = manifest.Tests.Select(file => JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(dataRoot, "tests", file)), AppJsonContext.Default.TestDefinition)!).ToList();
var dims = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(dataRoot, "dimensions.json")), AppJsonContext.Default.ListDimension)!;
var contexts = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(dataRoot, "contexts.json")), AppJsonContext.Default.ListSituationContext)!;
int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    passed++;
}
void Reject(Action action, string name)
{
    try { action(); }
    catch (FormatException) { passed++; return; }
    throw new Exception("FAIL: accepted " + name);
}
async Task StorageReject(Func<Task> action, string name)
{
    try { await action(); }
    catch (StorageException) { passed++; return; }
    throw new Exception("FAIL: " + name);
}
string Json(DeviceData d) => JsonSerializer.Serialize(d, AppJsonContext.Default.DeviceData);
TestDefinition Test(string id) => tests.Single(t => t.Id == id);
CatalogValidator.Validate(tests, dims, contexts);
Check(manifest.Tests.Distinct().Count() == tests.Count, "manifest has unique files");
Check(Directory.GetFiles(Path.Combine(dataRoot, "tests"), "*.json").Length == tests.Count + 1, "all tests are in manifest");

Answer AnswerFor(Question q) => q.Type switch
{
    QuestionType.Choice => new() { QuestionId = q.Id, ChoiceId = q.Options[0].Id },
    QuestionType.Pick => new() { QuestionId = q.Id, Picks = q.Options.Take(q.PickCount).Select(o => o.Id).ToList() },
    QuestionType.Allocation => new() { QuestionId = q.Id, Allocations = new() { [q.Options[0].Id] = q.Points } },
    QuestionType.Scale => new() { QuestionId = q.Id, ScaleValue = q.Min },
    QuestionType.Ladder => new() { QuestionId = q.Id, LadderLevel = -1 },
    QuestionType.Assign => new() { QuestionId = q.Id, Assignments = q.Rows.ToDictionary(r => r.Id, _ => q.Options[0].Id) },
    _ => throw new Exception("Unknown type")
};
TestResult Result(TestDefinition t, string person) => new()
{
    ProfileId = person, TestId = t.Id, TestVersion = t.Version, Definition = t,
    Answers = t.Questions.Select(AnswerFor).ToList(), Tallies = Scoring.Score(t, t.Questions.Select(AnswerFor))
};
foreach (var test in tests)
{
    foreach (var q in test.Questions)
    {
        Check(!Scoring.IsAnswered(q, null), test.Id + "/" + q.Id + " missing answer");
        Check(Scoring.IsAnswered(q, AnswerFor(q)), test.Id + "/" + q.Id + " valid answer");
        Check(Scoring.Contributions(q, AnswerFor(q)).Values.All(v => double.IsFinite(v) && Math.Abs(v) <= 1), "bounded score");
    }
    var person = new Person { Name = "Test" };
    var backup = new DeviceData { People = [person], Results = [Result(test, person.Id)] };
    Check(BackupValidator.Parse(Json(backup)).Results.Count == 1, test.Id + " snapshot roundtrip");
}
var scale = new Question { Type = QuestionType.Scale, Min = 1, Max = 7, Effects = new() { ["x"] = 1 } };
Check(Scoring.Contributions(scale, new() { ScaleValue = 1 })["x"] == -1, "scale low");
Check(Scoring.Contributions(scale, new() { ScaleValue = 4 })["x"] == 0, "scale middle");
Check(Scoring.Contributions(scale, new() { ScaleValue = 7 })["x"] == 1, "scale high");
Check(!Scoring.IsAnswered(scale, new() { ScaleValue = 8 }), "scale outside range rejected");
var pick = Test("lifeboat").Questions.First(q => q.Id == "board");
var pickAnswer = new Answer { Picks = pick.Options.Where(o => o.Id != "friend").Take(pick.PickCount - 1).Select(o => o.Id).Append("friend").ToList() };
Check(Math.Abs(Scoring.Contributions(pick, pickAnswer)["partiality"] - 3.0 / pick.PickCount) < 0.00001, "weighted lifeboat pick retained");
Check(!Scoring.IsAnswered(pick, new() { Picks = Enumerable.Repeat("friend", pick.PickCount).ToList() }), "duplicate picks rejected");
var assign = Test("leadership").Questions[0];
var assignment = AnswerFor(assign);
assignment.Assignments[assign.Rows[0].Id] = "missing";
Check(!Scoring.IsAnswered(assign, assignment), "unknown assignee rejected");
var allocation = tests.SelectMany(t => t.Questions).First(q => q.Type == QuestionType.Allocation);
Check(!Scoring.IsAnswered(allocation, new() { Allocations = new() { ["missing"] = allocation.Points } }), "unknown allocation rejected");
var ladder = tests.SelectMany(t => t.Questions).First(q => q.Type == QuestionType.Ladder);
Check(!Scoring.IsAnswered(ladder, new() { LadderLevel = ladder.Options.Count }), "ladder overflow rejected");
Check(Scoring.Contributions(ladder, new() { LadderLevel = -1 }).All(kv => Math.Abs(kv.Value + ladder.Effects[kv.Key]) < 0.00001), "ladder none endpoint");
Check(!Scoring.IsAnswered(Test("learning").Questions[0], new() { ChoiceId = "missing" }), "unknown choice rejected");

foreach (var invalid in new[] { "{}", "null", "[]", "{", "{\"schemaVersion\":2,\"people\":[],\"results\":[]}", "{\"schemaVersion\":1,\"people\":null,\"results\":[]}" })
    Reject(() => BackupValidator.Parse(invalid), "invalid backup");
var owner = new Person { Name = "A", PinnedTests = ["learning"] };
var good = new DeviceData { People = [owner], ActivePersonId = "stale", Results = [Result(Test("learning"), owner.Id)] };
Check(BackupValidator.Parse(Json(good)).ActivePersonId == owner.Id, "stale active ID repaired");
var duplicate = BackupValidator.Parse(Json(good)); duplicate.Results.Add(duplicate.Results[0]);
Reject(() => BackupValidator.Validate(duplicate), "duplicate result IDs");
var orphan = BackupValidator.Parse(Json(good)); orphan.Results[0].ProfileId = "absent";
Reject(() => BackupValidator.Validate(orphan), "orphan result");
var tampered = BackupValidator.Parse(Json(good)); tampered.Results[0].Tallies["learning"].Total = 0.123;
Reject(() => BackupValidator.Validate(tampered), "inconsistent tallies");
var nulls = Json(good).Replace("\"pinnedTests\":[\"learning\"]", "\"pinnedTests\":null");
Reject(() => BackupValidator.Parse(nulls), "null nested list");

var handler = new CatalogHandler(dataRoot);
var catalog = new TestCatalog(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/PhilosophyLab/") });
handler.FailOnce = true;
try { await catalog.GetTestsAsync(); throw new Exception("Expected initial catalog failure"); }
catch (HttpRequestException) { passed++; }
Check((await catalog.GetTestsAsync()).Count == tests.Count, "catalog retry after failure");
var js = new MemoryJs();
var store = new DeviceStore(js, new TestNavigation("https://example.test/PhilosophyLab/"), catalog);
var a = await store.GetActivePersonAsync();
await store.RenamePersonAsync(a.Id, "A");
var b = await store.AddPersonAsync("B");
var result = Result(Test("learning"), a.Id);
await store.SaveResultAsync(result);
Check((await store.GetResultsAsync(a.Id)).Count == 1 && (await store.GetResultsAsync(b.Id)).Count == 0, "attempt ownership unaffected by active person");
await store.SaveResultAsync(result);
Check((await store.GetResultsAsync(a.Id)).Count == 1, "save retries are idempotent");
var before = await store.ExportAsync();
js.FailWrites = true;
await StorageReject(() => store.RemovePersonAsync(a.Id), "quota failure propagated");
Check(await store.ExportAsync() == before, "quota failure preserves existing state");
await StorageReject(() => store.ImportAsync(Json(good)), "import write failure propagated");
Check(await store.ExportAsync() == before, "import failure is transactional");
Check(store.Warning is not null, "save failure visible");
js.FailWrites = false;
Check(await store.ImportAsync(Json(good)) == 1, "backup import");
Check(await store.ImportAsync(Json(good)) == 0, "duplicate import idempotent");
var pinned = BackupValidator.Parse(Json(good)); pinned.People[0].PinnedTests.Add("creativity");
await store.ImportAsync(Json(pinned));
Check((await store.GetAsync()).People.Single(p => p.Id == owner.Id).PinnedTests.Contains("creativity"), "pins merged");
Check(store.Warning is null, "successful retry clears warning");
var reload = new DeviceStore(js, new TestNavigation("https://example.test/PhilosophyLab/"), catalog);
Check((await reload.GetAsync()).Results.Count == 2, "reload persistence");
var other = new DeviceStore(js, new TestNavigation("https://example.test/another/"), catalog);
Check((await other.GetAsync()).Results.Count == 0, "project paths use separate keys");
js.Values["philosophy-tester/v1"] = Json(good);
Check(await other.ImportLegacyAsync() == 1 && js.Values.ContainsKey("philosophy-tester/v1"), "explicit legacy import preserves original");
var brokenJs = new MemoryJs();
var broken = new DeviceStore(brokenJs, new TestNavigation("https://example.test/"), catalog);
brokenJs.Values[broken.StorageKey] = "broken json";
await broken.GetAsync();
Check(broken.RecoveryJson == "broken json", "corrupt storage retained");
await StorageReject(() => broken.AddPersonAsync("C"), "corrupt storage cannot be overwritten");
await broken.ResetDamagedStorageAsync();
Check(broken.RecoveryJson is null && broken.Warning is null, "explicit reset recovers storage");
var blockedJs = new MemoryJs { FailReads = true, FailWrites = true };
var blocked = new DeviceStore(blockedJs, new TestNavigation("https://example.test/"), catalog);
await blocked.GetAsync();
Check(blocked.Warning is not null, "blocked reads visible");
await StorageReject(() => blocked.AddPersonAsync("C"), "blocked write visible");

var analyzer = new BehaviorAnalyzer(catalog);
var historical = Result(Test("learning"), a.Id);
historical.TestVersion = 999;
historical.Definition = null;
Check((await analyzer.ObserveAsync([historical])).Count == 0, "legacy mismatched definitions excluded");
historical.Definition = Test("learning");
Check((await analyzer.ObserveAsync([historical])).Count > 0, "snapshot used independently of current lookup");
var creativity = Test("creativity");
var failure = creativity.Questions[1];
Check(ScenarioSequence.Context(failure, [new() { QuestionId = "design", ChoiceId = "sketch" }])!.Contains("sketch"), "choice-dependent consequence");
Check(ScenarioSequence.Context(Test("leadership").Questions[1], [new() { QuestionId = "assign", Assignments = new() { ["core"] = "mike" } }])!.Contains("Mike"), "assignment-dependent consequence");
var evidence = await analyzer.EvidenceAsync([Result(Test("learning"), a.Id)]);
Check(evidence.Any(e => e.Chosen > 0 && e.Opportunities >= e.Chosen && e.Examples.All(x => x.ResultId.Length > 0)), "evidence has denominators and source links");
var growth = Result(Test("challenge"), a.Id);
growth.Baseline = new() { ["learning"] = new() { Total = 3, Weight = 3 } };
Check(BackupValidator.Parse(Json(new() { People = [a], Results = [growth] })).Results[0].Baseline!["learning"].Score == 1, "growth baseline roundtrip");
var malformedTest = JsonSerializer.Deserialize(JsonSerializer.Serialize(creativity, AppJsonContext.Default.TestDefinition), AppJsonContext.Default.TestDefinition)!;
malformedTest.Questions[1].Variants[0].QuestionId = "future-step";
Reject(() => CatalogValidator.ValidateTest(malformedTest), "forward consequence reference");

Console.WriteLine($"PASS: {passed} checks; {tests.Count} tests, {tests.Sum(t => t.Questions.Count)} questions, {dims.Count} dimensions. Six input types, backups, storage failures, ownership, history, evidence, and sequences verified.");

sealed class TestNavigation : NavigationManager
{
    public TestNavigation(string baseUri) => Initialize(baseUri, baseUri);
    protected override void NavigateToCore(string uri, bool forceLoad) { }
}

sealed class MemoryJs : IJSRuntime
{
    public Dictionary<string, string> Values { get; } = [];
    public bool FailWrites { get; set; }
    public bool FailReads { get; set; }
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var key = (string)args![0]!;
        if (identifier == "localStorage.getItem")
        {
            if (FailReads) throw new JSException("blocked");
            return ValueTask.FromResult((TValue)(object?)Values.GetValueOrDefault(key)!);
        }
        if (identifier == "localStorage.setItem")
        {
            if (FailWrites) throw new JSException("quota");
            Values[key] = (string)args[1]!;
            return ValueTask.FromResult(default(TValue)!);
        }
        throw new NotSupportedException(identifier);
    }
}

sealed class CatalogHandler(string root) : HttpMessageHandler
{
    public bool FailOnce { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (FailOnce) { FailOnce = false; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }
        var relative = request.RequestUri!.AbsolutePath.Split("/data/", 2)[1];
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(File.ReadAllText(Path.Combine(root, relative))) });
    }
}
