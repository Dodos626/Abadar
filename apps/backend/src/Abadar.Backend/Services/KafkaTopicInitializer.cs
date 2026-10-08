using Abadar.Backend.Events;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Abadar.Backend.Services;

// Creates required Version 2 topics before outbox publication begins.
public sealed class KafkaTopicInitializer(
    KafkaOptions options,
    KafkaRuntimeState runtimeState,
    ILogger<KafkaTopicInitializer> logger) : IHostedService
{
    // Creates topics idempotently and waits for Kafka during local startup.
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var admin = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = options.BootstrapServers,
                    SocketTimeoutMs = 5_000
                }).Build();
                await admin.CreateTopicsAsync(
                [
                    Topic(EventCatalog.OrdersTopic),
                    Topic(EventCatalog.TradesTopic)
                ]);
                runtimeState.MarkTopicsReady();
                logger.LogInformation("Kafka topics are ready at {BootstrapServers}", options.BootstrapServers);
                return;
            }
            catch (CreateTopicsException exception)
                when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                runtimeState.MarkTopicsReady();
                logger.LogInformation("Kafka topics already exist at {BootstrapServers}", options.BootstrapServers);
                return;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                runtimeState.MarkFailure(exception.Message);
                logger.LogWarning(exception, "Kafka topic initialization failed; retrying");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    // Requires no shutdown work because the admin client is method-scoped.
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Builds one topic specification using the configured local partition count.
    private TopicSpecification Topic(string name) => new()
    {
        Name = name,
        NumPartitions = options.TopicPartitions,
        ReplicationFactor = (short)options.ReplicationFactor
    };
}