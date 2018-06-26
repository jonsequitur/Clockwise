using System;

namespace Clockwise.Redis
{
    public sealed class RedisCircuitBreakerStorageSettings
    {
        public string ConnectionString { get; }
        public int DbId { get; }

        public RedisCircuitBreakerStorageSettings(string connectionString, int dbId)
        {
            if (String.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(connectionString));
            ConnectionString = connectionString;
            DbId = dbId;
        }
    }
}