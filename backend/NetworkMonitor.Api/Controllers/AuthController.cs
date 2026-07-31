using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<ActionResult<CurrentUserResponse>> Login(LoginRequest request)
    {
        var result = await signInManager.PasswordSignInAsync(request.Username.Trim(), request.Password, false, lockoutOnFailure: true);
        return result.Succeeded ? Ok(new CurrentUserResponse(request.Username.Trim())) : Unauthorized(new ProblemDetails { Status = 401, Title = "Login failed", Detail = "Invalid username or password." });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout() { await signInManager.SignOutAsync(); return NoContent(); }

    [HttpGet("me")]
    public ActionResult<CurrentUserResponse> Me() => Ok(new CurrentUserResponse(User.Identity!.Name!));
}
