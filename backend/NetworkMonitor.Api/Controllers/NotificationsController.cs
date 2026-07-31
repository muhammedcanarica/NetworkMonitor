using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetAll(
        [FromQuery] bool unreadOnly = false,
        [FromQuery, Range(1, NotificationService.MaximumLimit)] int limit = NotificationService.DefaultLimit,
        CancellationToken cancellationToken = default)
        => Ok(await notificationService.ListAsync(unreadOnly, limit, cancellationToken));

    [HttpGet("unread-count")]
    public async Task<ActionResult<NotificationUnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
        => Ok(new NotificationUnreadCountResponse(await notificationService.GetUnreadCountAsync(cancellationToken)));

    [HttpPut("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken cancellationToken)
        => await notificationService.MarkAsReadAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await notificationService.MarkAllAsReadAsync(cancellationToken);
        return NoContent();
    }
}
