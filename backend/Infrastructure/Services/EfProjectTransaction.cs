using Application.Features.Projects;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Services;

/// <summary>
/// EF Core transaction implementation shared by multi-step project workflows.
/// </summary>
internal sealed class EfProjectTransaction : IProjectTransaction
{
    private readonly IDbContextTransaction _transaction;
    private bool _committed;

    public EfProjectTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            await _transaction.RollbackAsync();
        }

        await _transaction.DisposeAsync();
    }
}