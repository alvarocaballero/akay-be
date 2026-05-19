// Ejemplo de como añadir un behavior a los ya existentes 

////using System.Diagnostics;
////using Akay.To.Core.Application.Mediator;
////using Microsoft.Extensions.Logging;
////namespace Akay.Be.Application.Behaviors;


/////// <summary>
/////// A behavior that measures the performance timing of request handling.
/////// </summary>
/////// <typeparam name="TRequest"></typeparam>
/////// <typeparam name="TResponse"></typeparam>
/////// <param name="logger"></param>
////public sealed class PerformanceTimingBehavior/<TRequest, TResponse>(ILogger<PerformanceTimingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
////    where TRequest : IRequest<TResponse>
////{
////    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
////    {
////        var stopwatch = Stopwatch.StartNew();
////        try
////        {
////            var response = await next().ConfigureAwait(false);
////            return response;
////        }
////        finally
////        {
////            stopwatch.Stop();
////            var requestName = typeof(TRequest).Name;
////            if (stopwatch.ElapsedMilliseconds > 500)
////            {
////                logger.LogWarning("Slow request {RequestName} took {ElapsedMs}ms.", requestName, stopwatch.ElapsedMilliseconds);
////            }
////            else
////            {
////                logger.LogDebug("Request {RequestName} took {ElapsedMs}ms.", requestName, stopwatch.ElapsedMilliseconds);
////            }
////        }
////    }
////}
