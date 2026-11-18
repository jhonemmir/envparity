using System.Text.Json.Serialization;

namespace EnvParity.Output;

// Declaration order and JsonPropertyOrder both fix the wire contract.
public sealed record JsonOutput(
    [property: JsonPropertyOrder(0)] int SchemaVersion,
    [property: JsonPropertyOrder(1)] string ToolVersion,
    [property: JsonPropertyOrder(2)] bool Valid,
    [property: JsonPropertyOrder(3)] bool Strict,
    [property: JsonPropertyOrder(4)] JsonFiles Files,
    [property: JsonPropertyOrder(5)] JsonSummary Summary,
    [property: JsonPropertyOrder(6)] IReadOnlyList<string> Missing,
    [property: JsonPropertyOrder(7)] IReadOnlyList<string> Empty,
    [property: JsonPropertyOrder(8)] IReadOnlyList<string> Extra,
    [property: JsonPropertyOrder(9)] IReadOnlyList<JsonIssue> Issues);

public sealed record JsonFiles(
    [property: JsonPropertyOrder(0)] string Env,
    [property: JsonPropertyOrder(1)] string Example);

public sealed record JsonSummary(
    [property: JsonPropertyOrder(0)] int Variables,
    [property: JsonPropertyOrder(1)] int Missing,
    [property: JsonPropertyOrder(2)] int Empty,
    [property: JsonPropertyOrder(3)] int Extra,
    [property: JsonPropertyOrder(4)] int Errors,
    [property: JsonPropertyOrder(5)] int Warnings);

public sealed record JsonIssue(
    [property: JsonPropertyOrder(0)] string Severity,
    [property: JsonPropertyOrder(1)] string Code,
    [property: JsonPropertyOrder(2)] string File,
    [property: JsonPropertyOrder(3)] int Line,
    [property: JsonPropertyOrder(4)] string Message);
