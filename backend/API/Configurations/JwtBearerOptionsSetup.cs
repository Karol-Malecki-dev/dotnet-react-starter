using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Shared.Settings;
using System.Text;

namespace API.Configurations
{

    /// <summary>
    /// Configures JwtBearerOptions from JwtSettings through the IOptions pattern.
    /// IConfigureNamedOptions keeps the setup compatible with DI and integration tests.
    /// </summary>
    public class JwtBearerOptionsSetup : IConfigureNamedOptions<JwtBearerOptions>
    {
        private readonly JwtSettings _jwtSettings;

        public JwtBearerOptionsSetup(IOptions<JwtSettings> jwtOptions)
        {
            _jwtSettings = jwtOptions.Value;
        }

        public void Configure(JwtBearerOptions options)
        {
            Configure(Options.DefaultName, options);
        }

        public void Configure(string? name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme)
            {
                return;
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));


            options.MapInboundClaims = false; // Preserve original JWT claim names such as sub and email.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role",
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.Zero // Do not add tolerance for clock differences.
            };
        }
    }
}
