using System.Net.Http.Json;
using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

/// <summary>
/// Loads definitions from wwwroot/data. To add a test, drop a JSON file in
/// wwwroot/data/tests and list its file name in wwwroot/data/tests/index.json.
/// </summary>
public sealed class TestCatalog(HttpClient http)
{
    private sealed record Catalog(List<Dimension> Dims, List<SituationContext> Contexts, List<TestDefinition> Tests);

    private Task<Catalog>? _load;

    public async Task<IReadOnlyList<TestDefinition>> GetTestsAsync() => (await LoadAsync()).Tests;

    public async Task<IReadOnlyList<TestDefinition>> GetTestsAsync(string lab) =>
        (await LoadAsync()).Tests.Where(t => t.Lab == lab).ToList();

    public async Task<IReadOnlyList<Dimension>> GetDimensionsAsync() => (await LoadAsync()).Dims;

    public async Task<IReadOnlyList<Dimension>> GetDimensionsAsync(string lab) =>
        (await LoadAsync()).Dims.Where(d => d.Lab == lab).ToList();

    public async Task<IReadOnlyList<SituationContext>> GetContextsAsync() => (await LoadAsync()).Contexts;

    public async Task<TestDefinition?> GetTestAsync(string id) =>
        (await LoadAsync()).Tests.FirstOrDefault(t => t.Id == id);

    private async Task<Catalog> LoadAsync()
    {
        var pending = _load ??= LoadCoreAsync();
        try { return await pending; }
        catch { if (ReferenceEquals(_load, pending)) _load = null; throw; }
    }

    private async Task<Catalog> LoadCoreAsync()
    {
        var dimsTask = http.GetFromJsonAsync("data/dimensions.json", AppJsonContext.Default.ListDimension);
        var ctxTask = http.GetFromJsonAsync("data/contexts.json", AppJsonContext.Default.ListSituationContext);
        var manifest = await http.GetFromJsonAsync("data/tests/index.json", AppJsonContext.Default.TestManifest) ?? new();

        var loads = manifest.Tests.Select(file =>
            http.GetFromJsonAsync($"data/tests/{file}", AppJsonContext.Default.TestDefinition));
        var tests = (await Task.WhenAll(loads)).OfType<TestDefinition>().ToList();

        var dims = await dimsTask ?? [];
        var contexts = await ctxTask ?? [];
        CatalogValidator.Validate(tests, dims, contexts);
        return new Catalog(dims, contexts, tests);
    }
}
