using PdfViewrMiniPr.Aplication.DTOs;
using PdfViewrMiniPr.Domain.Enums;

namespace PdfViewrMiniPr.Aplication.Interfaces;

public interface IUserService
{
    Task<UserDto> CreateUserAsync(CreateUserDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default);
}



