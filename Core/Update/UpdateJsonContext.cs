using System.Text.Json.Serialization;

namespace WinTempCleaner.Core.Update;

/// <summary>
/// Source-generated JSON metadata for the update subsystem.
/// Keeps Core reflection-free (NativeAOT/trimming friendly) by producing serializer
/// metadata at compile time instead of relying on runtime reflection.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UpdateManifest))]
[JsonSerializable(typeof(UpdateArtifactInfo))]
[JsonSerializable(typeof(TransactionJournal))]
public sealed partial class UpdateJsonContext : JsonSerializerContext
{
}
