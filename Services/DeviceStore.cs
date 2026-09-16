using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

public sealed class StorageException(string message) : Exception(message);

/// <summary>Transactional browser storage. A failed write never commits a partial in-memory change.</summary>
public sealed class DeviceStore(IJSRuntime js, NavigationManager navigation, TestCatalog catalog)
{
    private const string LegacyKey = "philosophy-tester/v1";
    public string StorageKey => $"philosophy-lab/v1:{new Uri(navigation.BaseUri).AbsolutePath}";
    private DeviceData? _data;
    private Task<DeviceData>? _load;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string? Warning { get; private set; }
    public string? RecoveryJson { get; private set; }
    public event Action? Changed;

    public Task<DeviceData> GetAsync() => _data is not null ? Task.FromResult(_data) : _load ??= LoadAsync();

    private async Task<DeviceData> LoadAsync()
    {
        string? json = null;
        try { json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey); }
        catch (JSException) { Warning = "Browser storage is unavailable. Changes cannot be saved until storage is available."; }
        try { _data = json is null ? new() : BackupValidator.Parse(json); }
        catch (FormatException)
        {
            RecoveryJson = json;
            Warning = "Stored data could not be read. It has been preserved. Download it from Saved before resetting storage.";
            _data = new();
        }
        EnsurePerson(_data);
        return _data;
    }

    public async Task<Person> GetActivePersonAsync()
    {
        var data = await GetAsync();
        return data.People.First(p => p.Id == data.ActivePersonId);
    }

    public async Task<List<TestResult>> GetResultsAsync(string personId) =>
        (await GetAsync()).Results.Where(r => r.ProfileId == personId).OrderByDescending(r => r.CompletedAt).ToList();

    public async Task<List<TestResult>> GetLatestResultsAsync(string personId) =>
        (await GetResultsAsync(personId)).GroupBy(r => r.TestId).Select(g => g.First()).ToList();

    public async Task<TestResult?> GetResultAsync(string id) => (await GetAsync()).Results.FirstOrDefault(r => r.Id == id);

    public Task SaveResultAsync(TestResult result) => ChangeAsync(data =>
    {
        if (!data.People.Any(p => p.Id == result.ProfileId)) throw new StorageException("The person who started this test was removed. Export these answers before leaving.");
        if (data.Results.All(r => r.Id != result.Id)) data.Results.Add(result);
    });

    public Task DeleteResultAsync(string id) => ChangeAsync(data => data.Results.RemoveAll(r => r.Id == id));

    public async Task<Person> AddPersonAsync(string name)
    {
        var person = new Person { Name = name.Trim() };
        await ChangeAsync(data => { data.People.Add(person); data.ActivePersonId = person.Id; });
        return person;
    }

    public Task RenamePersonAsync(string personId, string name) => ChangeAsync(data =>
    {
        var person = data.People.FirstOrDefault(p => p.Id == personId);
        if (person is not null) person.Name = name.Trim();
    });

    public Task SwitchPersonAsync(string personId) => ChangeAsync(data =>
    {
        if (data.People.Any(p => p.Id == personId)) data.ActivePersonId = personId;
    });

    public Task RemovePersonAsync(string personId) => ChangeAsync(data =>
    {
        data.People.RemoveAll(p => p.Id == personId);
        data.Results.RemoveAll(r => r.ProfileId == personId);
        EnsurePerson(data);
    });

    public Task TogglePinAsync(string testId) => ChangeAsync(data =>
    {
        var person = data.People.First(p => p.Id == data.ActivePersonId);
        if (!person.PinnedTests.Remove(testId)) person.PinnedTests.Add(testId);
    });

    public async Task<string> ExportAsync() => Serialize(await GetAsync());

    public async Task<int> ImportAsync(string json)
    {
        var incoming = BackupValidator.Parse(json);
        foreach (var r in incoming.Results.Where(r => r.Definition is null))
        {
            var current = await catalog.GetTestAsync(r.TestId);
            if (current?.Version == r.TestVersion) BackupValidator.ValidateAnswers(r, current);
        }
        int added = 0;
        await ChangeAsync(data =>
        {
            foreach (var p in incoming.People)
            {
                var existing = data.People.FirstOrDefault(e => e.Id == p.Id);
                if (existing is null) data.People.Add(p);
                else existing.PinnedTests = existing.PinnedTests.Union(p.PinnedTests).ToList();
            }
            var ids = data.Results.Select(r => r.Id).ToHashSet();
            var results = incoming.Results.Where(r => ids.Add(r.Id)).ToList();
            data.Results.AddRange(results);
            added = results.Count;
        });
        return added;
    }

    public async Task<int> ImportLegacyAsync()
    {
        string? json;
        try { json = await js.InvokeAsync<string?>("localStorage.getItem", LegacyKey); }
        catch (JSException) { throw new StorageException("The browser blocked access to the older storage."); }
        if (json is null) throw new FormatException("No older data found on this origin. Use a backup file if the site address changed.");
        return await ImportAsync(json);
    }

    public async Task ResetDamagedStorageAsync()
    {
        await GetAsync();
        if (RecoveryJson is null) return;
        await _gate.WaitAsync();
        try
        {
            var fresh = new DeviceData();
            EnsurePerson(fresh);
            await WriteAsync(fresh);
            _data = fresh;
            RecoveryJson = null;
            Warning = null;
        }
        finally { _gate.Release(); Changed?.Invoke(); }
    }

    private async Task ChangeAsync(Action<DeviceData> change)
    {
        await GetAsync();
        await _gate.WaitAsync();
        try
        {
            if (RecoveryJson is not null) throw new StorageException(Warning!);
            var next = BackupValidator.Parse(Serialize(_data!));
            change(next);
            BackupValidator.Validate(next);
            await WriteAsync(next);
            _data = next;
            Warning = null;
        }
        finally { _gate.Release(); Changed?.Invoke(); }
    }

    private async Task WriteAsync(DeviceData data)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", StorageKey, Serialize(data)); }
        catch (JSException)
        {
            Warning = "Save failed: browser storage is full or blocked. Existing data is unchanged. Export a backup and retry.";
            throw new StorageException(Warning);
        }
    }

    private static string Serialize(DeviceData data) => JsonSerializer.Serialize(data, AppJsonContext.Default.DeviceData);
    private static void EnsurePerson(DeviceData data)
    {
        if (data.People.Count == 0) data.People.Add(new Person { Name = "Me" });
        if (!data.People.Any(p => p.Id == data.ActivePersonId)) data.ActivePersonId = data.People[0].Id;
    }
}
