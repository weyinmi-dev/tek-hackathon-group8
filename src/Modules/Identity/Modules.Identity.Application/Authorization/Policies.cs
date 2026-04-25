namespace Modules.Identity.Application.Authorization;

public static class Policies
{
    public const string RequireEngineer = "RequireEngineer";
    public const string RequireManager  = "RequireManager";
    public const string RequireAdmin    = "RequireAdmin";
}

public static class Roles
{
    public const string Engineer = "engineer";
    public const string Manager  = "manager";
    public const string Admin    = "admin";
    public const string Viewer   = "viewer";
}
