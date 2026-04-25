using SharedKernel;

namespace Modules.Identity.Domain.Users;

public sealed record UserCreatedDomainEvent(Guid UserId, string Email, string Role) : IDomainEvent;
