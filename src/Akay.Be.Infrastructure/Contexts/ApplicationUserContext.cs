using System.Security.Claims;
using Akay.To.Core.Host.Contexts;
using Microsoft.AspNetCore.Http;

namespace Akay.Be.Infrastructure.Contexts;

public sealed class AkayUserContext(IHttpContextAccessor httpContextAccessor)
    : UserContextBase(httpContextAccessor)
{
}
