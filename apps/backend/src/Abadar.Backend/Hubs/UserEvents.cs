using Abadar.Backend.Models;
using Microsoft.AspNetCore.SignalR;

namespace Abadar.Backend.Hubs;

public interface IUserEvents
{
    Task OnUserCreatedAsync(UserResponse user, CancellationToken cancellationToken);
    Task OnUserUpdatedAsync(UserResponse user, CancellationToken cancellationToken);
    Task OnUserDeletedAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class SignalRUserEvents(IHubContext<UsersHub> hubContext) : IUserEvents
{
    public Task OnUserCreatedAsync(UserResponse user, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("user_created", user, cancellationToken);

    public Task OnUserUpdatedAsync(UserResponse user, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("user_updated", user, cancellationToken);

    public Task OnUserDeletedAsync(Guid userId, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("user_deleted", userId, cancellationToken);
}