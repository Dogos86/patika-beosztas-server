using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PatikaBeosztas.Infrastructure.Persistence;

public sealed class PostgreSqlReadyHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PatikaDbContext>();
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("A PostgreSQL kapcsolat elérhető.")
                : HealthCheckResult.Unhealthy("A PostgreSQL kapcsolat nem érhető el.");
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                "A PostgreSQL készenléti ellenőrzése sikertelen.",
                exception);
        }
    }
}
