using System.Data.Common;
using Npgsql;

namespace PatikaBeosztas.Infrastructure.Persistence;

public static class PostgreSqlErrorClassifier
{
    public static bool IsTransactionConflict(DbException exception) =>
        exception is PostgresException
        {
            SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
        };
}
