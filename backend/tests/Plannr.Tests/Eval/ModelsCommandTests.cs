using Plannr.Eval;
using Plannr.Infrastructure.Llm;
using Plannr.Infrastructure.Options;

namespace Plannr.Tests.Eval;

public sealed class ModelsCommandTests
{
    private sealed class FakeGroqApi(params string[] ids) : IGroqApi
    {
        public Task<GroqChatResponse> CreateChatCompletionAsync(GroqChatRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GroqModelList> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new GroqModelList { Data = ids.Select(id => new GroqModel { Id = id, OwnedBy = "groq", ContextWindow = 131072 }).ToList() });
    }

    [Fact]
    public async Task ListsModelsAndMarksTheConfiguredOnes()
    {
        var output = new StringWriter();

        await ModelsCommand.RunAsync(
            new FakeGroqApi("qwen/qwen3.8-27b", "some/vision-model"),
            new GroqOptions { ApiKey = "k", VisionModel = "some/vision-model" },
            output, CancellationToken.None);

        var text = output.ToString();
        Assert.Contains("qwen/qwen3.8-27b", text);
        Assert.Contains("<- Groq:TextModel", text);
        Assert.Contains("<- Groq:VisionModel", text);
        Assert.DoesNotContain("can't use", text);
    }

    [Fact]
    public async Task WarnsWhenAConfiguredModelIsUnavailableOrUnset()
    {
        var output = new StringWriter();

        await ModelsCommand.RunAsync(new FakeGroqApi("other/model"), new GroqOptions { ApiKey = "k" }, output, CancellationToken.None);

        var text = output.ToString();
        Assert.Contains("Groq:TextModel is 'qwen/qwen3.8-27b', which this key can't use", text);
        Assert.Contains("Groq:VisionModel is not set", text);
    }
}
