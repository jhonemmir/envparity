using EnvParity.Comparison;
using EnvParity.Cli;

namespace EnvParity.Output;

public interface IResultFormatter
{
    /// <summary>Returns the result without a trailing line break.</summary>
    string Format(
        ComparisonResult result,
        CliOptions options,
        bool useUnicode,
        bool useColor);
}
