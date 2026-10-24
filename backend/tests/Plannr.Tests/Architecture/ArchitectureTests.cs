using System.Reflection;
using System.Xml.Linq;
using NetArchTest.Rules;
using Plannr.Services.Services;
using Plannr.Tests.Retrieval;

namespace Plannr.Tests.Architecture;

/// <summary>
/// The rules the project layout relies on but the compiler can't enforce
/// (D42). Project boundaries already stop database code reaching the Worker
/// and browser code reaching the API; these three tests cover the rest:
///
/// 1. Inside Plannr.Services, only APIs/ makes network calls, so every call
///    goes through a Refit interface (with its timeouts, error handling and
///    a fake in tests) and everything else stays instant and offline.
/// 2. Plannr.Services only uses packages on an approved list, because rule 1
///    can't see inside a package that makes its own calls.
/// 3. The projects reference each other exactly as agreed (D41).
/// </summary>
public sealed class ArchitectureTests
{
    /// <summary>
    /// Types that open a connection. Reading the outcome of a call someone
    /// else made is fine (HttpStatusCode, HttpRequestException, Refit's
    /// ApiException): GroqLlmExtractor needs those for its retry rules.
    /// </summary>
    private static readonly string[] OpensConnections =
    [
        "System.Net.Http.HttpClient",
        "System.Net.Http.HttpClientHandler",
        "System.Net.Http.HttpMessageInvoker",
        "System.Net.Http.HttpMessageHandler",
        "System.Net.Http.DelegatingHandler",
        "System.Net.Http.SocketsHttpHandler",
        "System.Net.Http.IHttpClientFactory",
        "System.Net.Sockets",
        "System.Net.WebSockets",
        "System.Net.Dns",
        "System.Net.WebClient",
        "System.Net.WebRequest",
        "System.Net.HttpWebRequest"
    ];

    /// <summary>
    /// Where Plannr.Services may use those types: APIs/ makes the calls, and
    /// DependencyInjection/ configures the clients (base address, timeout,
    /// key) without ever sending anything.
    /// </summary>
    private static readonly string[] MayOpenConnections =
    [
        "Plannr.Services.APIs",
        "Plannr.Services.DependencyInjection"
    ];

    /// <summary>The packages Plannr.Services may use, and why each is safe there.</summary>
    private static readonly Dictionary<string, string> ApprovedServicesPackages = new()
    {
        ["Microsoft.Extensions.Configuration.Binder"] = "binds options sections; no I/O beyond reading configuration",
        ["Microsoft.Extensions.Http"] = "registers HttpClients for APIs/; DependencyInjection/ only",
        ["Microsoft.Extensions.Logging.Abstractions"] = "logging interfaces only",
        ["Refit.HttpClientFactory"] = "the APIs/ interfaces; rule 1 keeps its calls there",
        ["Serilog"] = "logging",
        ["Serilog.Sinks.Console"] = "writes to the console",
        ["Serilog.Sinks.File"] = "writes the local log file"
    };

    /// <summary>
    /// Who may reference whom (D41). The Worker's entry applies once its
    /// project exists. A new project has to be added here on purpose.
    /// </summary>
    private static readonly Dictionary<string, string[]> AgreedReferences = new()
    {
        ["Plannr.Services"] = [],
        ["Plannr.Scraping"] = ["Plannr.Services"],
        ["Plannr.Data"] = ["Plannr.Services"],
        ["Plannr.Api"] = ["Plannr.Data", "Plannr.Services"],
        ["Plannr.Worker"] = ["Plannr.Scraping", "Plannr.Services"],
        ["Plannr.Eval"] = ["Plannr.Scraping", "Plannr.Services"]
    };

    private static TestResult NetworkCallsOutside(Assembly assembly, string[] allowedNamespaces)
    {
        var types = Types.InAssembly(assembly).That().DoNotResideInNamespace(allowedNamespaces[0]);
        foreach (var allowed in allowedNamespaces.Skip(1))
        {
            types = types.And().DoNotResideInNamespace(allowed);
        }

        return types.Should().NotHaveDependencyOnAny(OpensConnections).GetResult();
    }

    [Fact]
    public void InServices_OnlyApisOpensNetworkConnections()
    {
        var result = NetworkCallsOutside(typeof(LinkClassifier).Assembly, MayOpenConnections);

        Assert.True(result.IsSuccessful,
            "These classes in Plannr.Services make network calls outside APIs/. Put the call behind a Refit " +
            "interface in APIs/ (or, for fetching shared links, in Plannr.Scraping) and inject that:\n  - "
            + string.Join("\n  - ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }

    /// <summary>
    /// Proves the rule above can fail: a rule that never matches anything
    /// would pass for ever. StubHttpHandler (a fake network for the tests)
    /// is an HttpMessageHandler, so the same check over this assembly must
    /// name it.
    /// </summary>
    [Fact]
    public void TheNetworkRule_CatchesAKnownOffender()
    {
        var result = NetworkCallsOutside(typeof(StubHttpHandler).Assembly, ["Plannr.Tests.NotARealNamespace"]);

        Assert.False(result.IsSuccessful);
        Assert.Contains(typeof(StubHttpHandler).FullName, result.FailingTypes.Select(t => t.FullName));
    }

    [Fact]
    public void Services_UsesOnlyApprovedPackages()
    {
        var used = Project("Plannr.Services").Descendants("PackageReference")
            .Select(p => (string)p.Attribute("Include")!)
            .ToList();

        var unapproved = used.Where(p => !ApprovedServicesPackages.ContainsKey(p)).ToList();

        Assert.True(unapproved.Count == 0,
            "Plannr.Services now uses packages that aren't on the approved list. If one makes network calls " +
            "itself (an SDK for some web service), use it from APIs/ only, then add it to " +
            "ApprovedServicesPackages with the reason it's safe:\n  - " + string.Join("\n  - ", unapproved));
    }

    [Fact]
    public void ProjectReferences_MatchTheAgreedLayout()
    {
        var problems = new List<string>();

        foreach (var project in Directory.GetDirectories(Path.Combine(Backend, "src")).Select(Path.GetFileName))
        {
            if (!File.Exists(Path.Combine(Backend, "src", project!, $"{project}.csproj")))
            {
                continue;
            }

            var actual = Project(project!).Descendants("ProjectReference")
                .Select(r => Path.GetFileNameWithoutExtension((string)r.Attribute("Include")!))
                .Order()
                .ToArray();

            if (!AgreedReferences.TryGetValue(project!, out var agreed))
            {
                problems.Add($"{project} is a new project: add it to AgreedReferences (and D41) on purpose.");
            }
            else if (!actual.SequenceEqual(agreed.Order()))
            {
                problems.Add($"{project} references [{string.Join(", ", actual)}]; agreed: [{string.Join(", ", agreed)}].");
            }
        }

        Assert.True(problems.Count == 0, "Project references out of step with the agreed layout:\n  - "
            + string.Join("\n  - ", problems));
    }

    private static XDocument Project(string name) =>
        XDocument.Load(Path.Combine(Backend, "src", name, $"{name}.csproj"));

    /// <summary>The backend/ folder, found from the test output by walking up to Plannr.slnx.</summary>
    private static string Backend { get; } = FindBackend();

    private static string FindBackend()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "Plannr.slnx")))
        {
            directory = directory.Parent
                ?? throw new InvalidOperationException("Plannr.slnx not found above the test output.");
        }

        return directory.FullName;
    }
}
