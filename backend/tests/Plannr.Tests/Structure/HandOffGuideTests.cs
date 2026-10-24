using System.Text.RegularExpressions;

namespace Plannr.Tests.Structure;

/// <summary>
/// Keeps docs/guides/hand-offs.md and the hand-off TODOs in the code in
/// step, the way StructureManifestTests does for placeholders: a TODO with
/// no entry in the guide's checklist, or a ticked entry whose TODO is still
/// in the code, fails here.
/// </summary>
public sealed partial class HandOffGuideTests
{
    private const string GuidePath = "docs/guides/hand-offs.md";

    private static readonly Lazy<string> Root = new(() =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, GuidePath)))
        {
            directory = directory.Parent
                ?? throw new InvalidOperationException($"{GuidePath} not found above {AppContext.BaseDirectory}.");
        }

        return directory.FullName;
    });

    /// <summary>Every TODO(Hx.y) id in backend/src, with the file it's in.</summary>
    private static Dictionary<string, string> TodosInCode()
    {
        var found = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root.Value, "backend", "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            foreach (Match match in Todo().Matches(File.ReadAllText(file)))
            {
                found.TryAdd(match.Groups[1].Value, Path.GetRelativePath(Root.Value, file).Replace('\\', '/'));
            }
        }

        return found;
    }

    /// <summary>Checklist lines: "- [ ] **H1.2** ..." (to do) or "- [x] **H1.2** ..." (done).</summary>
    private static Dictionary<string, bool> Checklist() =>
        ChecklistLine().Matches(File.ReadAllText(Path.Combine(Root.Value, GuidePath)))
            .ToDictionary(m => m.Groups["id"].Value, m => m.Groups["done"].Value is "x" or "X");

    [Fact]
    public void EveryTodoInTheCode_IsInTheChecklist()
    {
        var checklist = Checklist();

        var problems = TodosInCode()
            .Where(todo => !checklist.ContainsKey(todo.Key))
            .Select(todo => $"{todo.Key} (in {todo.Value}) has no '- [ ] **{todo.Key}**' line in {GuidePath}.")
            .ToList();

        Assert.True(problems.Count == 0, "Hand-off TODOs missing from the guide:\n  - " + string.Join("\n  - ", problems));
    }

    [Fact]
    public void TheChecklist_MatchesWhatsLeftInTheCode()
    {
        var todos = TodosInCode();
        var problems = new List<string>();

        foreach (var (id, done) in Checklist())
        {
            if (done && todos.TryGetValue(id, out var file))
            {
                problems.Add($"{id} is ticked, but its TODO is still in {file}. Finish it, or untick it.");
            }
            else if (!done && !todos.ContainsKey(id))
            {
                problems.Add($"{id} is gone from the code: tick it ('- [x]').");
            }
        }

        Assert.True(problems.Count == 0, $"{GuidePath} out of step with the code:\n  - " + string.Join("\n  - ", problems));
    }

    [GeneratedRegex(@"TODO\((H\d+\.\d+)\)")]
    private static partial Regex Todo();

    [GeneratedRegex(@"^- \[(?<done>[ xX])\] \*\*(?<id>H\d+\.\d+)\*\*", RegexOptions.Multiline)]
    private static partial Regex ChecklistLine();
}
