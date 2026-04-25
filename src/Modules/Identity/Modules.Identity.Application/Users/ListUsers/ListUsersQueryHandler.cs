using Application.Abstractions.Messaging;
using Modules.Identity.Domain.Users;
using SharedKernel;

namespace Modules.Identity.Application.Users.ListUsers;

internal sealed class ListUsersQueryHandler(IUserRepository users)
    : IQueryHandler<ListUsersQuery, IReadOnlyList<UserListItem>>
{
    public async Task<Result<IReadOnlyList<UserListItem>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<User> all = await users.ListAsync(cancellationToken);
        IReadOnlyList<UserListItem> items = all
            .Select(u => new UserListItem(u.Id, u.Email, u.FullName, u.Handle, u.Role, u.Team, u.Region, u.LastLoginAtUtc))
            .ToList();
        return Result.Success(items);
    }
}
