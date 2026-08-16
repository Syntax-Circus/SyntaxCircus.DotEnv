namespace SyntaxCircus.DotEnv.Tests;

public class DotEnvOptionsTests
{
    [Fact]
    public void Enabled_DefaultsToNull()
    {
        var options = new DotEnvOptions();

        options.Enabled.ShouldBeNull();
    }

    [Fact]
    public void Enabled_IsSettable()
    {
        var options = new DotEnvOptions { Enabled = true };

        options.Enabled.ShouldBe(true);
    }
}
