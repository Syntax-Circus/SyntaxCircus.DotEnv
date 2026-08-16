namespace SyntaxCircus.DotEnv.Tests;

public class ShouldLoadDotEnvTests
{
    private static IConfiguration ConfigurationWith(bool? enabled)
    {
        var dict = new Dictionary<string, string?>();
        if (enabled.HasValue)
        {
            dict["DotEnv:Enabled"] = enabled.Value.ToString();
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static IHostEnvironment FakeEnvironment(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    [Fact]
    public void NullConfiguration_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            DotEnvConfigurationExtensions.ShouldLoadDotEnv(null!, FakeEnvironment("Development")));
    }

    [Fact]
    public void NullEnvironment_ThrowsArgumentNullException()
    {
        var configuration = ConfigurationWith(null);

        Should.Throw<ArgumentNullException>(() => configuration.ShouldLoadDotEnv(null!));
    }

    [Fact]
    public void ExplicitlyEnabled_TrueRegardlessOfEnvironment()
    {
        var configuration = ConfigurationWith(true);

        configuration.ShouldLoadDotEnv(FakeEnvironment("Production")).ShouldBeTrue();
    }

    [Fact]
    public void ExplicitlyDisabled_FalseRegardlessOfEnvironment()
    {
        var configuration = ConfigurationWith(false);

        configuration.ShouldLoadDotEnv(FakeEnvironment("Development")).ShouldBeFalse();
    }

    [Fact]
    public void Unset_DevelopmentEnvironment_ReturnsTrue()
    {
        var configuration = ConfigurationWith(null);

        configuration.ShouldLoadDotEnv(FakeEnvironment("Development")).ShouldBeTrue();
    }

    [Fact]
    public void Unset_NonDevelopmentEnvironment_ReturnsFalse()
    {
        var configuration = ConfigurationWith(null);

        configuration.ShouldLoadDotEnv(FakeEnvironment("Production")).ShouldBeFalse();
    }
}
