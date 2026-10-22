using System.Text.Json;

namespace Plannr.Tests.Structure;

/// <summary>
/// Keeps docs/structure.json (the list of everything planned but not built)
/// and the placeholder files on disk in step, so moving, renaming or deleting
/// planned pieces can't leave hanging files or broken links behind. Each test
/// collects every problem before failing, so one run lists them all.
///
/// It reads the whole repository, not just backend/: mobile/, infra/ and
/// data-pool/ placeholders are checked here too, because this is the test
/// suite CI already runs on every push.
/// </summary>
public sealed class StructureManifestTests
{
    private const string ManifestPath = "docs/structure.json";

    private static readonly string[] Kinds = ["stub", "generated", "folder"];

    /// <summary>File types a placeholder can be: ones whose first line can be a comment and still be valid.</summary>
    private static readonly string[] StubExtensions = [".cs", ".ts", ".tsx", ".js", ".yml", ".yaml", ".dockerfile", ".md"];

    /// <summary>Never scanned for placeholders: build output, dependencies, tool state.</summary>
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", ".expo", "bin", "obj", "node_modules", "TestResults", "logs"
    };

    private sealed record Entry(string Path, string Kind, string Milestone, string Purpose, string[]? Connects, string? Question);

    private sealed record Manifest(Dictionary<string, string> Milestones, List<Entry> Entries);

    private static readonly Lazy<(string Root, Manifest Manifest)> Loaded = new(Load);

    private static string Root => Loaded.Value.Root;

    private static List<Entry> Entries => Loaded.Value.Manifest.Entries;

    private static (string, Manifest) Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, ManifestPath)))
        {
            directory = directory.Parent
                ?? throw new InvalidOperationException($"{ManifestPath} not found above {AppContext.BaseDirectory}.");
        }

        var json = File.ReadAllText(Path.Combine(directory.FullName, ManifestPath));
        var manifest = JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"{ManifestPath} is empty.");

        return (directory.FullName, manifest);
    }

    private static string OnDisk(string path) => Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));

    private static string? FirstLine(string file) =>
        File.ReadLines(file).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

    private static void AssertNone(List<string> problems, string advice) =>
        Assert.True(problems.Count == 0, $"{advice}\n  - {string.Join("\n  - ", problems)}");

    [Fact]
    public void Entries_AreWellFormed()
    {
        var problems = new List<string>();

        foreach (var entry in Entries)
        {
            if (!Kinds.Contains(entry.Kind))
            {
                problems.Add($"{entry.Path}: kind '{entry.Kind}' is not one of {string.Join(", ", Kinds)}.");
            }

            if (!Loaded.Value.Manifest.Milestones.ContainsKey(entry.Milestone))
            {
                problems.Add($"{entry.Path}: milestone '{entry.Milestone}' isn't in the milestones list.");
            }

            if (string.IsNullOrWhiteSpace(entry.Purpose))
            {
                problems.Add($"{entry.Path}: no purpose.");
            }

            if (entry.Path.Contains('\\') || entry.Path.StartsWith('/') || entry.Path.EndsWith('/'))
            {
                problems.Add($"{entry.Path}: use forward slashes, relative to the repo root, no leading or trailing slash.");
            }

            if (entry.Kind == "stub" && !StubExtensions.Contains(Path.GetExtension(entry.Path), StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"{entry.Path}: a stub can't be this file type (its first line must be a comment). Make it 'generated'.");
            }
        }

        problems.AddRange(Entries.GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: listed {g.Count()} times."));

        AssertNone(problems, $"{ManifestPath} has malformed entries:");
    }

    [Fact]
    public void EveryStub_ExistsAndStillCarriesItsMarker()
    {
        var problems = new List<string>();

        foreach (var stub in Entries.Where(e => e.Kind == "stub"))
        {
            var file = OnDisk(stub.Path);
            if (!File.Exists(file))
            {
                problems.Add($"{stub.Path}: missing. Moved or deleted? Update or remove its entry.");
                continue;
            }

            var marker = $"PLANNED ({stub.Milestone})";
            if (FirstLine(file)?.Contains(marker, StringComparison.Ordinal) != true)
            {
                problems.Add($"{stub.Path}: first line doesn't contain '{marker}'. Built now? Remove its entry. Milestone changed? Update both.");
            }
        }

        AssertNone(problems, "Stubs out of step with the list:");
    }

    [Fact]
    public void EveryFolder_Exists()
    {
        var problems = Entries
            .Where(e => e.Kind == "folder" && !Directory.Exists(OnDisk(e.Path)))
            .Select(e => $"{e.Path}: folder missing.")
            .ToList();

        AssertNone(problems, "Planned folders missing:");
    }

    [Fact]
    public void GeneratedPaths_DontExistYet()
    {
        var problems = Entries
            .Where(e => e.Kind == "generated" && (File.Exists(OnDisk(e.Path)) || Directory.Exists(OnDisk(e.Path))))
            .Select(e => $"{e.Path}: now exists. The tool has run: remove its 'generated' entry.")
            .ToList();

        AssertNone(problems, "Generated paths that have been generated:");
    }

    [Fact]
    public void EveryPlaceholderOnDisk_IsListed()
    {
        var listed = Entries.Where(e => e.Kind == "stub")
            .Select(e => e.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var problems = PlaceholderFiles()
            .Where(path => !listed.Contains(path))
            .Select(path => $"{path}: has a PLANNED marker but no entry. Add one, or delete the file.")
            .ToList();

        AssertNone(problems, "Placeholders not in the list (hanging files):");
    }

    [Fact]
    public void EveryConnection_PointsAtSomethingReal()
    {
        var listed = Entries.Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var problems = Entries
            .SelectMany(e => (e.Connects ?? []).Select(target => (e.Path, Target: target)))
            .Where(c => !listed.Contains(c.Target) && !File.Exists(OnDisk(c.Target)) && !Directory.Exists(OnDisk(c.Target)))
            .Select(c => $"{c.Path} -> {c.Target}: neither a list entry nor a file in the repo.")
            .ToList();

        AssertNone(problems, "Connections to things that don't exist:");
    }

    /// <summary>Every file in the repo (outside build output and tool folders) whose first line is a PLANNED marker.</summary>
    private static IEnumerable<string> PlaceholderFiles()
    {
        var pending = new Stack<string>([Root]);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (!SkippedFolders.Contains(Path.GetFileName(child)))
                {
                    pending.Push(child);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (StubExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
                    && new FileInfo(file).Length < 4096
                    && FirstLine(file)?.Contains("PLANNED (", StringComparison.Ordinal) == true)
                {
                    yield return Path.GetRelativePath(Root, file).Replace(Path.DirectorySeparatorChar, '/');
                }
            }
        }
    }
}
