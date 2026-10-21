using System.Text.RegularExpressions;
using Plannr.Core.Diagnostics;

namespace Plannr.Tests.Diagnostics;

/// <summary>
/// ErrorReference keeps its program code in a static field, so these tests
/// share state. The collection stops xUnit running them in parallel with
/// anything else that touches it.
/// </summary>
[Collection(ErrorReferenceCollection.Name)]
public sealed class ErrorReferenceTests : IDisposable
{
    public void Dispose() => ErrorReference.Reset();

    [Fact]
    public void New_AfterConfigure_HasIdProgramAndLogCodes()
    {
        ErrorReference.Configure(ErrorReference.WorkerProgram, ErrorReference.MainLog);

        var reference = ErrorReference.New();

        Assert.Matches(new Regex("^[0-9a-f]{8}:02:01$"), reference);
    }

    [Fact]
    public void New_BeforeConfigure_SaysItIsUnconfigured()
    {
        ErrorReference.Reset();

        Assert.EndsWith(":00:00", ErrorReference.New());
    }

    [Fact]
    public void New_TwiceInARow_GivesDifferentIds()
    {
        Assert.NotEqual(ErrorReference.New(), ErrorReference.New());
    }

    [Theory]
    [InlineData("1", "01")]
    [InlineData("001", "01")]
    [InlineData("0a", "01")]
    [InlineData("01", "")]
    public void Configure_WithCodesThatAreNotTwoDigits_Throws(string program, string log)
    {
        Assert.Throws<ArgumentException>(() => ErrorReference.Configure(program, log));
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ErrorReferenceCollection
{
    public const string Name = "ErrorReference (static state)";
}
