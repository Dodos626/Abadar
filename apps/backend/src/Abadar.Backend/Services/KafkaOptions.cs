namespace Abadar.Backend.Services;

// Stores Kafka connectivity and Version 2 topic settings.
public sealed record KafkaOptions(
    string BootstrapServers,
    bool Enabled,
    int TopicPartitions,
    int ReplicationFactor)
{
    // Loads local or container Kafka settings from application configuration.
    public static KafkaOptions FromConfiguration(IConfiguration configuration) => new(
        configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "localhost:9092",
        !string.Equals(configuration["KAFKA_ENABLED"], "false", StringComparison.OrdinalIgnoreCase),
        ParsePositive(configuration["KAFKA_TOPIC_PARTITIONS"], 3),
        ParsePositive(configuration["KAFKA_REPLICATION_FACTOR"], 1));

    // Parses a positive integer while retaining a safe local default.
    private static int ParsePositive(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}