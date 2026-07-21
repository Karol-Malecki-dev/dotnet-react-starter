using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shared.Dtos;
using Shared.Responses;
using Shared.Settings;

namespace API.Controllers;

/// <summary>
/// Exposes non-sensitive runtime settings for the frontend bootstrap process.
/// </summary>
[ApiController]
[Route("api/runtime-config")]
public class RuntimeConfigController : ControllerBase
{
    private readonly EmailDeliverySettings _emailDeliverySettings;
    private readonly EmailTwoFactorSettings _emailTwoFactorSettings;

    public RuntimeConfigController(
        IOptions<EmailDeliverySettings> emailDeliveryOptions,
        IOptions<EmailTwoFactorSettings> emailTwoFactorOptions)
    {
        _emailDeliverySettings = emailDeliveryOptions.Value;
        _emailTwoFactorSettings = emailTwoFactorOptions.Value;
    }

    /// <summary>
    /// Returns the runtime configuration used by the frontend to decide which UI features should be available.
    /// </summary>
    /// <returns>A standardized API response containing feature flags.</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AppRuntimeConfigurationDto>), StatusCodes.Status200OK)]
    public IActionResult GetRuntimeConfiguration()
    {
        var response = new AppRuntimeConfigurationDto
        {
            Features = new AppFeatureFlagsDto
            {
                EmailDeliveryEnabled = _emailDeliverySettings.Enabled,
                EmailTwoFactorEnabled = _emailTwoFactorSettings.Enabled,
                EmailTwoFactorEnabledForNewUsers = _emailTwoFactorSettings.EnableForNewUsers
            }
        };

        return Ok(ApiResponse<AppRuntimeConfigurationDto>.Success(response, "Runtime configuration loaded", StatusCodes.Status200OK));
    }
}