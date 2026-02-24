namespace Docket.Api.Services;

/// <summary>
/// Abstracts the identity of the currently authenticated caller.
/// In v1: StubCurrentUserService returns a hardcoded user ID for development.
/// In production: replace with JwtCurrentUserService that reads from the Bearer token.
///
/// Register as a scoped service. All endpoints use this interface — never
/// read HttpContext.User directly in endpoint handlers.
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
}

/// <summary>
/// Development stub. Returns a fixed user ID.
/// Replace this registration in Program.cs when real auth is introduced.
/// </summary>
public class StubCurrentUserService : ICurrentUserService
{
    // This ID must exist in the database. The seed/migration creates this user.
    public static readonly Guid StubUserId = new("00000000-0000-0000-0000-000000000001");
    public Guid UserId => StubUserId;
}
