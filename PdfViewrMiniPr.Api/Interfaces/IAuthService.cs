using PdfViewrMiniPr.Aplication.DTOs;

namespace PdfViewrMiniPr.Aplication.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
}


