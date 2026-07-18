using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Common;

namespace PaieEducation.Infrastructure.Persistence;

internal sealed class DapperUnitOfWork : IUnitOfWork
{
    private readonly SqliteConnection _connection;
    private SqliteTransaction? _transaction;

    public DapperUnitOfWork(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    internal SqliteTransaction? Transaction => _transaction;

    public async Task BeginAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Une transaction est déjà active.");
        _transaction = (SqliteTransaction)(await _connection.BeginTransactionAsync(ct));
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Aucune transaction active à valider.");
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Aucune transaction active à annuler.");
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void Dispose() => _transaction?.Dispose();
}
