using MediatR;
using NexusFlow.AppCore.DTOs.Sales;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Commands
{
    public class CreateInvoiceCommand : IRequest<Result<int>>
    {
        public CreateInvoiceRequest Invoice { get; set; }
    }
}
