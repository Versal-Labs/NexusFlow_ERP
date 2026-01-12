using MediatR;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Queries
{
    public class GetTrialBalanceQuery : IRequest<Result<TrialBalanceReport>>
    {
        public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
    }
}
