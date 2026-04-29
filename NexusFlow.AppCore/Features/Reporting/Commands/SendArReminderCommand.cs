using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Reporting.Commands
{
    public class SendArReminderCommand : IRequest<Result<bool>>
    {
        public int CustomerId { get; set; }
    }

    public class SendArReminderHandler : IRequestHandler<SendArReminderCommand, Result<bool>>
    {
        private readonly IErpDbContext _context;
        private readonly ISmsGatewayService _smsService;

        public SendArReminderHandler(IErpDbContext context, ISmsGatewayService smsService)
        {
            _context = context;
            _smsService = smsService;
        }

        public async Task<Result<bool>> Handle(SendArReminderCommand request, CancellationToken cancellationToken)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
            if (customer == null || string.IsNullOrEmpty(customer.Phone))
                return Result<bool>.Failure("Customer not found or no phone number on file.");

            var companyName = await _context.SystemConfigs.Where(x => x.Key == "Company.Name").Select(x => x.Value).FirstOrDefaultAsync(cancellationToken);
            // Calculate total outstanding quickly
            var outstandingBalance = await _context.SalesInvoices
                .Where(i => i.CustomerId == request.CustomerId && i.IsPosted && i.PaymentStatus != InvoicePaymentStatus.Paid)
                .SumAsync(i => i.GrandTotal - i.AmountPaid, cancellationToken);

            if (outstandingBalance <= 0)
                return Result<bool>.Failure("Customer has no outstanding balance.");

            // Construct Message
            string message = $"Dear {customer.Name}, your account has an outstanding balance of LKR {outstandingBalance:N2}. Please arrange payment at your earliest convenience. Thank you. - {companyName}";

            // Fire SMS
            bool sent = await _smsService.SendSmsAsync(customer.Phone, message);

            return sent ? Result<bool>.Success(true, "SMS Reminder Sent Successfully.") : Result<bool>.Failure("SMS Gateway failed to send message.");
        }
    }
}
