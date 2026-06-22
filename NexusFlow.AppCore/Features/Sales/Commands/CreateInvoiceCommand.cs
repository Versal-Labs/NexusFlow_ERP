using MediatR;
using NexusFlow.AppCore.DTOs.Sales;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Commands
{
    public class CreateInvoiceCommand : IRequest<Result<int>>, IFinancialPeriodControlledRequest
    {
        public CreateInvoiceRequest Invoice { get; set; }
        public DateTime FinancialDate => Invoice.Date;
    }
}
