using Microsoft.EntityFrameworkCore;
using PdfViewrMiniPr.Domain.Entities;
using PdfViewrMiniPr.Infrastructure.Database;

namespace PdfViewrMiniPr.Infrastructure.Repositories;

public interface IUserRepository : IRepositoryBase<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public class UserRepository : RepositoryBase<User>, IUserRepository
{
    public UserRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return DbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }
}



