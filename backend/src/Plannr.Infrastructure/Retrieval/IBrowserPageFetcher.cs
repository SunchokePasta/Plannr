namespace Plannr.Infrastructure.Retrieval;

/// <summary>
/// An <see cref="IPageFetcher"/> that runs the page's JavaScript before
/// reading it. Its own interface so the container can tell it apart from
/// the plain HTTP fetcher, and so retriever tests can fake it.
/// </summary>
public interface IBrowserPageFetcher : IPageFetcher;
