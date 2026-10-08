using Abadar.Backend.Models;
using Microsoft.AspNetCore.SignalR;

namespace Abadar.Backend.Hubs;

public interface ISimulationProgressEvents
{
    Task PublishAsync(SimulationProgressUpdate update, CancellationToken cancellationToken);
}

// Broadcasts one simulation's progress to the SignalR group named by its run ID.
public sealed class SignalRSimulationProgressEvents(IHubContext<SimulationHub> hubContext)
    : ISimulationProgressEvents
{
    // Sends a progress update only to clients observing the matching run ID.
    public Task PublishAsync(SimulationProgressUpdate update, CancellationToken cancellationToken) =>
        hubContext.Clients.Group(update.SimulationId.ToString())
            .SendAsync("simulation_progress", update, cancellationToken);
}