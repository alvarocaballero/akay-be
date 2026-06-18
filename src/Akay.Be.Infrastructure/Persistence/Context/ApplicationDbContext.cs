using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.EF.Infrastructure.DbContexts;
using Akay.To.EF.Infrastructure.ModelBuilding;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Context;

public sealed class ApplicationDbContext(IUserContext userContext,
                                         DbContextRegistration<ApplicationDbContext> registration,
                                         DbContextOptions<ApplicationDbContext> options)
    : BaseDbContext<ApplicationDbContext>(userContext, registration, options)
{

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyDbContextSettings(this);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
