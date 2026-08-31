using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Modules.Projects.Invitations;

internal static class ProjectInvitationToken
{
    public static (string RawToken, string TokenHash) Create()
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (rawToken, Hash(rawToken));
    }

    public static string Hash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
