using MediatR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Finance.Queries
{
    public sealed record GetFinancialPeriodStatusQuery(DateTime Date) : IRequest<Result<FinancialPeriodStatusDto>>;

    public sealed class GetFinancialPeriodStatusHandler
        : IRequestHandler<GetFinancialPeriodStatusQuery, Result<FinancialPeriodStatusDto>>
    {
        private readonly IFinancialPeriodService _periodService;

        public GetFinancialPeriodStatusHandler(IFinancialPeriodService periodService) => _periodService = periodService;

        public async Task<Result<FinancialPeriodStatusDto>> Handle(GetFinancialPeriodStatusQuery request, CancellationToken cancellationToken) =>
            Result<FinancialPeriodStatusDto>.Success(await _periodService.GetStatusAsync(request.Date, cancellationToken));
    }
}
