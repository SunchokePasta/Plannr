using Microsoft.Extensions.Configuration;
using Plannr.Core.Options;
using Plannr.Infrastructure.Options;

namespace Plannr.Tests.Options;

public sealed class ConfigurationExtensionsTests
{
    private sealed class SampleOptions : AppOptionsBase
    {
        public const string SectionName = "Sample";

        public int TimeoutSeconds { get; set; } = 30;

        public override void Validate()
        {
            if (TimeoutSeconds < 1)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:{nameof(TimeoutSeconds)} must be at least 1, not {TimeoutSeconds}.");
            }
        }
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void GetValidated_BindsTheSection()
    {
        var options = Config(("Sample:TimeoutSeconds", "5"))
            .GetValidated<SampleOptions>(SampleOptions.SectionName);

        Assert.Equal(5, options.TimeoutSeconds);
    }

    [Fact]
    public void GetValidated_WithNoSection_GivesDefaults()
    {
        var options = Config().GetValidated<SampleOptions>(SampleOptions.SectionName);

        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Fact]
    public void GetValidated_WithABadValue_ThrowsNamingTheKey()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Config(("Sample:TimeoutSeconds", "0")).GetValidated<SampleOptions>(SampleOptions.SectionName));

        Assert.Contains("Sample:TimeoutSeconds", exception.Message);
    }
}
