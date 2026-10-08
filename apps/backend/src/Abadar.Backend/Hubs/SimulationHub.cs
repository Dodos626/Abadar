using Abadar.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Abadar.Backend.Hubs;

// Streams administrator-only simulation progress to the browser in real time.
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class SimulationHub : Hub
{
    // Subscribes the current connection to one simulation's isolated progress stream.
    public Task WatchSimulation(Guid simulationId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, simulationId.ToString());

    // Removes the current connection from a completed simulation stream.
    public Task StopWatchingSimulation(Guid simulationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, simulationId.ToString());
}