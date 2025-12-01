using Microsoft.AspNetCore.Mvc;
using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Aplication.Interfaces;
using PdfViewrMiniPr.Domain.Enums;

namespace PdfViewerMiniPr.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto dto, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateUserAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _userService.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("users/internal")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetInternalUsers(CancellationToken cancellationToken)
    {
        var users = await _userService.GetByRoleAsync(UserRole.InternalUser, cancellationToken);
        return Ok(users);
    }
}



