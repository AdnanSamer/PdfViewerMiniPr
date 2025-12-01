using PdfViewrMiniPr.Domain.Enums;

namespace PdfViewrMiniPr.Aplication.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUserDto
{
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Password { get; set; } = default!;
    public UserRole Role { get; set; }
}



