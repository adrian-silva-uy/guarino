using System.Security.Claims;

namespace GuarinoApp.Application.Common.Interfaces;
public interface ICurrentUserService
    {
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    IEnumerable<Claim> Claims { get; }
    }
