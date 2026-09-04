using Microsoft.EntityFrameworkCore;

namespace ProofPath.Infrastructure.Persistence;

public class ProofPathDbContext : DbContext
{
    public ProofPathDbContext(DbContextOptions<ProofPathDbContext> options)
        : base(options)
    {
    }
}
