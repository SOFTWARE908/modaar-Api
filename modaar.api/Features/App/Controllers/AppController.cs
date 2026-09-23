namespace modaar.api.Features.App.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using modaar.api.Features.App.Dtos;

    [ApiController]
    [Route("api/app")]
    [Produces("application/json")]
    public class AppController : ControllerBase
    {
        // IOptionsSnapshot, not IOptions: appsettings can be edited on the server and
        // picked up without an app-pool recycle.
        private readonly IOptionsSnapshot<AppVersionOptions> _options;

        public AppController(IOptionsSnapshot<AppVersionOptions> options) => _options = options;

        // Anonymous: the splash screen runs before anyone has signed in, and a forced
        // update has to reach users whose token has expired.
        [AllowAnonymous]
        [HttpGet("minimum-version")]
        [ProducesResponseType(typeof(MinimumVersionDto), StatusCodes.Status200OK)]
        public IActionResult GetMinimumVersion()
        {
            var o = _options.Value;

            return Ok(new MinimumVersionDto
            {
                MinimumRequiredVersion = o.MinimumRequiredVersion,
                MinimumVersionRequiredMessage = o.MinimumVersionRequiredMessage,
                MinimumVersionOptional = o.MinimumVersionOptional,
                MinimumVersionOptionalMessage = o.MinimumVersionOptionalMessage,
                StartDate = o.StartDate,
                EndDate = o.EndDate
            });
        }
    }
}