namespace EnvParity.Parsing;

/// <summary>
/// A variable defined in a file. <see cref="Line"/> is the line of the first
/// definition; a duplicate only replaces <see cref="Value"/> and <see cref="IsEmpty"/>.
/// </summary>
public sealed record EnvVariable(string Name, string Value, int Line, bool IsEmpty);
