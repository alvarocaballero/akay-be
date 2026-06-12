using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.EF.Infrastructure.DbContexts;
using Akay.To.EF.Infrastructure.ModelBuilding;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Context;

public sealed class ApplicationDbContext(IUserContext userContext,
                                         EFSupportSettings<ApplicationDbContext> supportSettings,
                                         DbContextOptions<ApplicationDbContext> options)
    : BaseDbContext<ApplicationDbContext>(userContext, supportSettings, options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyEFSupport(this);

        base.OnModelCreating(modelBuilder);
    }
}
