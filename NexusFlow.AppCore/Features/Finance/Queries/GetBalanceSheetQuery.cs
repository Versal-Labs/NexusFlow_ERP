using MediatR;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Queries
{
    public class GetBalanceSheetQuery : IRequest<Result<BalanceSheetReport>>
    {
        public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
    }
}
