using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Behaviors;

public sealed class MigrationRetirementBehavior<TRequest, TResponse>(IErpDbContext db) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request.GetType().Name is not "ImportTrialBalanceCommand" and not "ImportOpenInvoicesCommand" and not "ExecuteLegacyProductsCommand" and not "ImportRawMaterialStockCommand" and not "PostOpeningStockCommand" || db.MigrationProjects == null || !await db.MigrationProjects.AnyAsync(ct)) return await next();
        const string message = "Opening imports have moved to Administration → Migration Centre. The old import cannot bypass the saved project's approval and reconciliation controls.";
        var type = typeof(TResponse); var result = Activator.CreateInstance(type);
        type.GetProperty(nameof(Result.Succeeded))?.SetValue(result, false); type.GetProperty(nameof(Result.Message))?.SetValue(result, message); type.GetProperty(nameof(Result.Code))?.SetValue(result, "migration_centre_required");
        return (TResponse)result!;
    }
}
