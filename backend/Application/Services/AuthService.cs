using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Enums;
using Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class AuthService : IJwtTokenService
    {


        public AuthService() { }

        public Task<JwtTokens> GenerateTokensAsync(User user)
        {
            throw new NotImplementedException();
        }

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            throw new NotImplementedException();
        }

        public Task<JwtTokens?> RefreshTokensAsync(string refreshToken)
        {
            throw new NotImplementedException();
        }

        public Task RevokeTokenAsync(string refreshToken)
        {
            throw new NotImplementedException();
        }

        public Task RevokeAllUserTokensAsync(Guid userId, RevocationReason reason)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsTokenRevokedAsync(string refreshToken)
        {
            throw new NotImplementedException();
        }
    }
}
