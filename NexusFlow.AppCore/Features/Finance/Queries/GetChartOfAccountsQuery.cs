using MediatR;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Queries
{
    public class GetChartOfAccountsQuery : IRequest<Result<List<AccountDto>>>
    {
        // No parameters needed as we want the full tree
    }
}
