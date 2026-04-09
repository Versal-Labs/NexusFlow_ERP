using MediatR;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Finance.Commands
{
    public class CreateAccountCommand : IRequest<Result<int>>
    {
        public string Code { get; set; } = string.Empty; // e.g., "5030"
        public string Name { get; set; } = string.Empty; // e.g., "Advertising"
        public AccountType Type { get; set; }            // Asset=1, Liability=2...
        public int? ParentAccountId { get; set; }        // Optional (Null for Root)

        // If true, we can post invoices to it. If false, it's a folder.
        public bool IsTransactionAccount { get; set; }
        public bool RequiresReconciliation { get; set; }
    }
}
