using EduFlyUp.Repositories;

namespace EduFlyUp.Tests;

// InMemory cannot model SQL transactions. This lock only verifies service/HTTP
// sequencing; AdminUserSqlServerTests verifies the production transaction guard.
internal sealed class TestAdminUserStatusLock : IAdminUserStatusLock, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<IAdminUserStatusTransaction> BeginAsync()
    {
        await _gate.WaitAsync();
        return new Transaction(_gate);
    }
    public void Dispose() => _gate.Dispose();
    private sealed class Transaction(SemaphoreSlim gate) : IAdminUserStatusTransaction
    {
        public Task CommitAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() { gate.Release(); return ValueTask.CompletedTask; }
    }
}
