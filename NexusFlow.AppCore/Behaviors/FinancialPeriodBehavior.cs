using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Behaviors
{
    public sealed class FinancialPeriodBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IFinancialPeriodService _periodService;

        public FinancialPeriodBehavior(IFinancialPeriodService periodService) => _periodService = periodService;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request is not IFinancialPeriodControlledRequest controlled)
                return await next();

            var status = await _periodService.GetStatusAsync(controlled.FinancialDate, cancellationToken);
            if (status.CanCreateTransactions)
                return await next();

            // Accounting invariant: reject before handlers consume a sequence or mutate stock/ledger state.
            var failure = CreateFailure(status.Message);
            if (failure != null)
                return failure;

            throw new InvalidOperationException(status.Message);
        }

        private static TResponse? CreateFailure(string message)
        {
            var responseType = typeof(TResponse);
            object? result = null;

            if (responseType == typeof(Result))
            {
                result = Result.Failure(message);
            }
            else if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                result = Activator.CreateInstance(responseType);
                responseType.GetProperty(nameof(Result<int>.Succeeded))?.SetValue(result, false);
                responseType.GetProperty(nameof(Result<int>.Message))?.SetValue(result, message);
                responseType.GetProperty(nameof(Result<int>.Errors))?.SetValue(result, new[] { message });
            }

            responseType.GetProperty(nameof(Result.Code))?.SetValue(result, "financial_period_not_open");
            return result == null ? default : (TResponse)result;
        }
    }
}
