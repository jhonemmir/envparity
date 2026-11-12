namespace EnvParity.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void TestRunner_RunsOnNet10()
    {
        Assert.Equal(10, Environment.Version.Major);
        Assert.Equal("EnvParity.Tests", typeof(SmokeTests).Assembly.GetName().Name);
    }
}
