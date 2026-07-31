using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/notification-settings/email")]
public sealed class EmailNotificationSettingsController(IEmailNotificationSettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EmailNotificationSettingsResponse>> Get(CancellationToken cancellationToken)
        => Ok(await service.GetAsync(cancellationToken));

    [HttpPut]
    public async Task<ActionResult<EmailNotificationSettingsResponse>> Update(
        UpdateEmailNotificationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await service.UpdateAsync(request, cancellationToken)); }
        catch (EmailNotificationValidationException exception) { return BadRequest(Problem("Invalid email notification settings", exception.Message)); }
    }

    [HttpPost("test")]
    public async Task<ActionResult<TestEmailResponse>> SendTest(CancellationToken cancellationToken)
    {
        try
        {
            await service.SendTestAsync(cancellationToken);
            return Ok(new TestEmailResponse("Test email sent."));
        }
        catch (EmailNotificationValidationException exception) { return BadRequest(Problem("Invalid email notification settings", exception.Message)); }
        catch (EmailNotificationOperationException exception) { return BadRequest(Problem("Test email failed", exception.Message)); }
    }

    private static ProblemDetails Problem(string title, string detail) => new() { Status = 400, Title = title, Detail = detail };
}
