namespace Plannr.Tests.HandOffs;

/// <summary>
/// Why a hand-off test is skipped: the TODO it waits for. When you finish
/// one, search for its constant (Todo.H1_3, say), delete the
/// "(Skip = Todo.H1_3)" from each test that uses it, and run them. Once
/// nothing uses a constant, delete it too.
/// </summary>
internal static class Todo
{
    private const string Guide = " (docs/guides/hand-offs.md)";

    public const string H1_3 = "Un-skip after TODO(H1.1)-(H1.3), the wake signal" + Guide;
    public const string H2_1 = "Un-skip after TODO(H2.1), the secret check" + Guide;
    public const string H2_2 = "Un-skip after TODO(H2.2), the API's secret filter" + Guide;
    public const string H3_1 = "Un-skip after TODO(H3.1), accepting a link" + Guide;
    public const string H3_2 = "Un-skip after TODO(H3.2), nudging the worker" + Guide;
    public const string H3_3 = "Un-skip after TODO(H3.3), the claim endpoint" + Guide;
    public const string H4_1 = "Un-skip after TODO(H4.1), the wake endpoint" + Guide;
    public const string H4_3 = "Un-skip after TODO(H4.3), the job loop itself" + Guide;
    public const string H4_5 = "Un-skip after TODO(H4.2)-(H4.5), the slots and a round of claims" + Guide;
    public const string H4_6 = "Un-skip after TODO(H4.6), the error boundary" + Guide;
    public const string H4_7 = "Un-skip after TODO(H4.6)-(H4.7), the error boundary and handing back" + Guide;
    public const string H5_1 = "Un-skip after TODO(H5.1), the result endpoint" + Guide;
    public const string H5_2 = "Un-skip after TODO(H5.2), the announcer's loop" + Guide;
    public const string H5_3 = "Un-skip after TODO(H5.3), one round of announcements" + Guide;
    public const string H6_1 = "Un-skip after TODO(H6.1), the worker's wiring" + Guide;
    public const string H6_2 = "Un-skip after TODO(H6.2), the API's wiring" + Guide;
}
