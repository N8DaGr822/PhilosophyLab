using System.Text.Json.Serialization;
using PhilosophyTester.Models;

namespace PhilosophyTester.Services;

// Source-generated JSON keeps serialization working after WebAssembly trimming.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<Dimension>))]
[JsonSerializable(typeof(List<SituationContext>))]
[JsonSerializable(typeof(TestManifest))]
[JsonSerializable(typeof(TestDefinition))]
[JsonSerializable(typeof(DeviceData))]
[JsonSerializable(typeof(List<Answer>))]
internal partial class AppJsonContext : JsonSerializerContext;
