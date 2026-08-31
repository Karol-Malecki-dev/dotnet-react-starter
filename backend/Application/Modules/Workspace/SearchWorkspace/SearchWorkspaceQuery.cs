namespace Application.Modules.Workspace.SearchWorkspace;

public sealed record SearchWorkspaceQuery(
    Guid UserId,
    string Query,
    string Type = "projectTask",
    int Page = 1,
    int PageSize = 10);

public sealed record WorkspaceSearchResult(
    string Type,
    Guid ResourceId,
    Guid ProjectId,
    string Title,
    string Context);

public sealed record WorkspaceSearchPage(
    IReadOnlyList<WorkspaceSearchResult> Items,
    int Page,
    int PageSize,
    int TotalCount);

public interface ISearchWorkspaceStore
{
    Task<WorkspaceSearchPage> SearchAsync(SearchWorkspaceQuery query, CancellationToken cancellationToken = default);
}

public interface ISearchWorkspaceHandler
{
    Task<WorkspaceSearchPage> HandleAsync(SearchWorkspaceQuery query, CancellationToken cancellationToken = default);
}

public sealed class SearchWorkspaceHandler : ISearchWorkspaceHandler
{
    private readonly ISearchWorkspaceStore _store;

    public SearchWorkspaceHandler(ISearchWorkspaceStore store)
    {
        _store = store;
    }

    public Task<WorkspaceSearchPage> HandleAsync(SearchWorkspaceQuery query, CancellationToken cancellationToken = default)
        => _store.SearchAsync(query, cancellationToken);
}
