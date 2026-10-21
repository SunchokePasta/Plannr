using Plannr.Eval;

namespace Plannr.Tests.Eval;

public sealed class EvalArgumentsTests
{
    [Theory]
    [InlineData]
    [InlineData("help")]
    [InlineData("--help")]
    public void NoCommand_IsHelp(params string[] args)
    {
        Assert.Equal(EvalCommand.Help, EvalArguments.Parse(args).Command);
    }

    [Fact]
    public void ExtractAUrl()
    {
        var parsed = EvalArguments.Parse(["extract", "https://venue.example.com/e/1"]);

        Assert.Equal(new EvalArguments(EvalCommand.ExtractLink, Url: new Uri("https://venue.example.com/e/1")), parsed);
    }

    [Fact]
    public void ExtractAnImage()
    {
        var parsed = EvalArguments.Parse(["extract", "--image", "shot.png"]);

        Assert.Equal(EvalCommand.ExtractImage, parsed.Command);
        Assert.Equal("shot.png", parsed.ImagePath);
    }

    [Fact]
    public void Verbose_CanGoAnywhere()
    {
        Assert.True(EvalArguments.Parse(["--verbose", "models"]).Verbose);
        Assert.True(EvalArguments.Parse(["models", "--VERBOSE"]).Verbose);
    }

    [Theory]
    [InlineData("extract", "venue.example.com/e/1")]
    [InlineData("extract")]
    [InlineData("scrape", "https://x.example")]
    [InlineData("models", "extra")]
    public void AnythingElse_SaysWhy(params string[] args)
    {
        var exception = Assert.Throws<ArgumentException>(() => EvalArguments.Parse(args));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }
}
