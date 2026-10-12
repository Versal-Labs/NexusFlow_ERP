using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.Services;
using NexusFlow.Shared.Wrapper;
namespace NexusFlow.AppCore.Features.Sales.Queries;
public class GetUnpaidInvoicesHandler(IErpDbContext db) : IRequestHandler<GetUnpaidInvoicesQuery, Result<UnpaidInvoicesResponseDto>>
{
    public async Task<Result<UnpaidInvoicesResponseDto>> Handle(GetUnpaidInvoicesQuery request, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct)) return Result<UnpaidInvoicesResponseDto>.Failure("Customer not found.");
        var balance = await new CustomerBalanceService(db).GetAsync(request.CustomerId, DateTime.MaxValue.Date.AddDays(-1), ct);
        return Result<UnpaidInvoicesResponseDto>.Success(new() {
            NetBalance = balance.NetBalance, UnappliedCredit = balance.UnappliedCredit, OpeningCredits = balance.OpeningCredits.Where(x => x.Available > 0).ToList(),
            Invoices = balance.Debits.Where(x => x.Balance > 0).OrderBy(x => x.DueDate).ThenBy(x => x.Id).Select(x => new UnpaidInvoiceDto {
                Id = x.Id, TargetType = x.TargetType, InvoiceNumber = x.Reference, InvoiceDate = x.DocumentDate, DueDate = x.DueDate,
                GrandTotal = x.Amount, AmountPaid = x.Settled, Balance = x.Balance }).ToList()
        });
    }
}
