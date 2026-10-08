namespace Abadar.Backend.Services;

// Shares Kafka startup and publisher status with readiness and admin APIs.
public sealed class KafkaRuntimeState
{
    private volatile bool _topicsReady;
    private volatile bool _publisherConnected;
    private string? _lastError;

    public bool TopicsReady => _topicsReady;
    public bool PublisherConnected => _publisherConnected;
    public string? LastError => _lastError;

    // Records successful topic initialization.
    public void MarkTopicsReady()
    {
        _topicsReady = true;
        _lastError = null;
    }

    // Records successful outbox publication connectivity.
    public void MarkPublisherConnected()
    {
        _publisherConnected = true;
        _lastError = null;
    }

    // Records a Kafka failure for readiness diagnostics.
    public void MarkFailure(string error)
    {
        _publisherConnected = false;
        _lastError = error;
    }
}