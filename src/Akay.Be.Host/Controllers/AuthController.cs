using Akay.Be.Application.Features.Auth;
using Akay.To.Azure.Host.Security.EntraId;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Akay.Be.Host.Controllers;

/// <summary>
/// Controlador para la gestión de autenticación.
/// </summary>
/// <param name="dispatcher"></param>
/// <param name="entraIdClaimsReader"></param>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public sealed class AuthController(IDispatcher dispatcher,
                                   EntraIdClaimsReader entraIdClaimsReader) : ControllerBase
{
    /// <summary>
    /// Intercambia un token válido de Entra ID por un JWT propio de Akay.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("exchange")]
    [Authorize(AuthenticationSchemes = EntraIdSchemeNames.EntraId)]
    [EndpointSummary("Intercambia un token válido de Entra ID por un JWT propio de Akay.")]
    [ProducesResponseType<ExchangeAkayTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IResult> Exchange(CancellationToken cancellationToken)
    {
        if (!entraIdClaimsReader.TryRead(User, out var userInfo) || userInfo is null)
        {
            return TypedResults.Problem(title: "auth.exchange.invalid_entra_claims",
                                        detail: "El token de Entra ID no contiene los claims requeridos para el intercambio.",
                                        statusCode: StatusCodes.Status403Forbidden);
        }

        return (await dispatcher.Send(new ExchangeEntraTokenCommand(userInfo.ExternalId,
                                                                    userInfo.Email,
                                                                    userInfo.Name,
                                                                    userInfo.GivenName,
                                                                    userInfo.FamilyName),
                                      cancellationToken)).ToOk();
    }
}

