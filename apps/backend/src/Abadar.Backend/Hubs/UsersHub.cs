using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Abadar.Backend.Models;

namespace Abadar.Backend.Hubs;

[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class UsersHub : Hub;