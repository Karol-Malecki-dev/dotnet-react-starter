using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Shared.Settings;
using System.Text;

namespace API.Configurations
{

    /// <summary>
    /// Konfiguruje JwtBearerOptions z JwtSettings (IOptions pattern).
    /// Używa IConfigureNamedOptions aby działać poprawnie z DI i testami integracyjnymi.
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


            options.MapInboundClaims = false; // Zachowaj oryginalne nazwy claimów (sub, email itd.) od AI , sam nie wiedziałem o co chodzi z tym konkretnie :(
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
                ClockSkew = TimeSpan.Zero // Brak tolerancji na różnicę czasu między serwerem a klientem
            };
        }
    }
}
