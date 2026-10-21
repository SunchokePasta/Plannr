using System.Text.Json;
using Plannr.Infrastructure.Llm;
using Plannr.Infrastructure.Options;

namespace Plannr.Tests.Llm;

public sealed class GroqOptionsTests
{
    [Fact]
    public void Validate_WithoutAKey_SaysHowToSetOne()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new GroqOptions().Validate());

        Assert.Contains("Groq:ApiKey", exception.Message);
        Assert.Contains("user-secrets", exception.Message);
    }

    [Fact]
    public void Validate_WithAKeyAndDefaults_Passes()
    {
        new GroqOptions { ApiKey = "test-key" }.Validate();
    }

    [Theory]
    [InlineData("qwen/qwen3.8-27b", "none")]
    [InlineData("openai/gpt-oss-120b", "low")]
    [InlineData("meta-llama/some-model", null)]
    public void ReasoningEffortFor_DependsOnTheModel(string model, string? expected)
    {
        Assert.Equal(expected, new GroqOptions().ReasoningEffortFor(model));
    }

    [Fact]
    public void ReasoningEffortFor_UsesTheConfiguredValueWhenSet()
    {
        Assert.Equal("medium", new GroqOptions { ReasoningEffort = "medium" }.ReasoningEffortFor("qwen/qwen3.8-27b"));
    }

    [Fact]
    public void AnImageMessage_IsWrittenAsContentParts()
    {
        var message = new GroqChatMessage
        {
            Content = new List<GroqContentPart>
            {
                GroqContentPart.ForText("Read this"),
                GroqContentPart.ForImage([1, 2, 3], "image/png")
            }
        };

        var json = JsonSerializer.Serialize(message);

        Assert.Contains("\"type\":\"image_url\"", json);
        Assert.Contains("data:image/png;base64,AQID", json);
        Assert.DoesNotContain("\"text\":null", json);
    }

    [Fact]
    public void ATextMessage_IsWrittenAsAPlainString()
    {
        var json = JsonSerializer.Serialize(new GroqChatMessage { Role = "system", Content = "Hello" });

        Assert.Equal("""{"role":"system","content":"Hello"}""", json);
    }
}
