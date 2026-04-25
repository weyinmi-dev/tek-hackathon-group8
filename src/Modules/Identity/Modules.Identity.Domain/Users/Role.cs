namespace Modules.Identity.Domain.Users;

public static class Roles
{
    public const string Engineer = "engineer";
    public const string Manager = "manager";
    public const string Admin = "admin";
    public const string Viewer = "viewer";

    public static readonly IReadOnlyCollection<string> All = [Engineer, Manager, Admin, Viewer];

    public static bool IsValid(string role) => All.Contains(role, StringComparer.OrdinalIgnoreCase);
}
