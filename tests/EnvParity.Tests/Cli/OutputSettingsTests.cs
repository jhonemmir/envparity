using EnvParity.Cli;

namespace EnvParity.Tests.Cli;

public sealed class OutputSettingsTests
{
    [Fact]
    public void ShouldUseColor_EnabledByDefault() =>
        Assert.True(OutputSettings.ShouldUseColor(
            noColorOption: false, noColorEnvironment: null, redirected: false));

    [Fact]
    public void ShouldUseColor_DisabledByOption() =>
        Assert.False(OutputSettings.ShouldUseColor(
            noColorOption: true, noColorEnvironment: null, redirected: false));

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData(" ")]
    public void ShouldUseColor_DisabledByNonEmptyNoColor(string value) =>
        Assert.False(OutputSettings.ShouldUseColor(
            noColorOption: false, noColorEnvironment: value, redirected: false));

    [Fact]
    public void ShouldUseColor_EmptyNoColorIsIgnored() =>
        Assert.True(OutputSettings.ShouldUseColor(
            noColorOption: false, noColorEnvironment: string.Empty, redirected: false));

    [Fact]
    public void ShouldUseColor_DisabledWhenRedirected() =>
        Assert.False(OutputSettings.ShouldUseColor(
            noColorOption: false, noColorEnvironment: null, redirected: true));
}
