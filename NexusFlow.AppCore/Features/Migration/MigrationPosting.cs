using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.Domain.Common;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.Inventory;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;

namespace NexusFlow.AppCore.Features.Migration;

public sealed partial class MigrationCentreService
{
    public async Task ApproveAsync(int id, MigrationApproveRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Evidence)) throw new InvalidOperationException("Record approval evidence, including physical cheque verification and closing control totals where relevant.");
        using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.LockMigrationProjectAsync(id, ct);
        var p = await Project(id, true, ct); await Editable(p, ct);
        var preview = await ValidateAsync(id, request.Selection, ct);
        if (!preview.CanApprove || preview.Hash != request.Hash) throw new InvalidOperationException("Validation failed or the preview changed. Validate and review the current data again.");
        p.ApprovedHash = preview.Hash; p.ApprovedBy = ActorId; p.ApprovedAt = DateTime.UtcNow;
        Audit(id, "Approved", request.Evidence + "; hash " + preview.Hash); await CompleteSimpleOperationAsync(ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<int> ExecuteAsync(int id, MigrationSelection selection, CancellationToken ct)
    {
        int runId;
        using (var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            await db.LockMigrationProjectAsync(id, ct);
            var p = await Project(id, true, ct); await Editable(p, ct);
            var preview = await ValidateAsync(id, selection, ct);
            if (!preview.CanApprove || p.ApprovedHash != preview.Hash || p.ApprovedAt == null) throw new InvalidOperationException("The current selection must be validated and approved before execution.");
            var run = new MigrationRun { ProjectId = id, ApprovedHash = preview.Hash };
            db.MigrationRuns.Add(run); Audit(id, "Execution queued", preview.Hash); await db.SaveChangesAsync(ct); runId = run.Id;
            db.MigrationRunPayloads.Add(new() { RunId = runId, PayloadJson = MigrationJson.Write(new MigrationRunPayload { Selection = selection, Preview = preview }) });
            if (owningOperationId is { } operationId)
            {
                var op = await db.MigrationOperations.SingleAsync(x => x.Id == operationId, ct); op.RunId = runId;
                var targets = preview.Units.Where(x => x.Status == "Pending").ToList(); op.Total = targets.Count;
                for (var i = 0; i < targets.Count; i++) db.MigrationOperationItems.Add(new() { OperationId = operationId, Ordinal = i, Module = targets[i].Module, Kind = MigrationCatalog.ScopeKind(targets[i].Module), Code = targets[i].Scope, Rows = targets[i].Rows.Count });
            }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        // The durable run is created first. If enqueue fails, the administrator can retry dispatch.
        if (owningOperationId == null) await DispatchAsync(id, runId, ct); return runId;
    }
    public async Task DispatchAsync(int id, int runId, CancellationToken ct)
    {
        using var worker = await db.AcquireMigrationWorkerAsync(id, ct);
        using var dispatch = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.LockMigrationProjectAsync(id, ct);
        await Project(id, true, ct); var run = await db.MigrationRuns.SingleAsync(x => x.Id == runId && x.ProjectId == id, ct);
        if (await db.MigrationOperations.AnyAsync(x => x.RunId == runId, ct)) throw new InvalidOperationException("Resume the owning operation from its progress panel.");
        using var ownership = OwnRun(runId); await Editable(await Project(id, true, ct), ct);
        if (run.Status == "Completed") return;
        if (run.JobId != null)
        {
            if (!jobs.LookupState(run.JobId).ConfirmedInactive) throw new InvalidOperationException("The background job is active or its state cannot be confirmed. Check the job service before retrying.");
        }
        run.Status = "Queued"; run.Error = null; await db.SaveChangesAsync(ct);
        worker.Dispose(); // The project transaction still guards claim; a new worker needs this lease.
        try { run.JobId = jobs.EnqueueAsync<MigrationExecutionJob>(job => job.RunAsync(runId)); await db.SaveChangesAsync(ct); await dispatch.CommitAsync(ct); }
        catch { run.Status = "Failed"; run.Error = "Background job could not be dispatched. Use Resume run after the job service is available."; await db.SaveChangesAsync(ct); await dispatch.CommitAsync(ct); throw; }
    }
    public async Task ExecuteRunAsync(int runId, CancellationToken ct)
    {
        MigrationRun run; MigrationProject p;
        var projectId = await db.MigrationRuns.AsNoTracking().Where(x => x.Id == runId).Select(x => x.ProjectId).SingleAsync(ct);
        IDisposable? worker = null;
        if (owningOperationId == null)
        {
            if (await db.MigrationOperations.AnyAsync(x => x.RunId == runId, ct)) return;
            try { worker = await db.AcquireMigrationWorkerAsync(projectId, ct); }
            catch (InvalidOperationException) { return; }
        }
        using var workerLease = worker;
        using var ownership = OwnRun(runId);
        using (var claim = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            await db.LockMigrationProjectAsync(projectId, ct);
            run = await db.MigrationRuns.SingleAsync(x => x.Id == runId, ct);
            if (run.Status == "Completed") return;
            if (run.Status != "Queued") return; // Duplicate delivery never changes another worker's active/failed run.
            p = await Project(run.ProjectId, true, ct);
            await Editable(p, ct);
            if (p.ApprovedHash != run.ApprovedHash) throw new InvalidOperationException("The approved project changed; this run must not execute.");
            run.Status = "Running"; await db.SaveChangesAsync(ct); await claim.CommitAsync(ct);
        }
        var payload = await ReadRunPayloadAsync(run, ct);
        // One party's selected components or all layers of one SKU share a database transaction.
        var groups = payload.Preview.Units.Where(x => x.Status == "Pending").GroupBy(x => x.Module.EndsWith("Master") ? "Master/" + x.Module + "/" + x.Scope : MigrationCatalog.ScopeKind(x.Module) + "/" + x.Scope)
            .OrderBy(g => g.Min(x => Array.IndexOf(MigrationCatalog.Modules.Keys.ToArray(), x.Module))).ToList();
        try
        {
            foreach (var group in groups)
            {
                if (owningOperationId is { } currentOperationId)
                {
                    var current = await db.MigrationOperations.SingleAsync(x => x.Id == currentOperationId, ct);
                    current.Phase = "Importing selected units"; current.CurrentItem = group.First().Module + "/" + group.First().Scope; current.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
                try
                {
                    using var scope = scopeFactory.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<MigrationCentreService>();
                    service.owningOperationId = owningOperationId; service.owningRunId = runId; service.operationActor = operationActor;
                    await service.ExecutePostingGroupAsync(runId, group.ToList(), ct);
                }
                catch (InvalidOperationException ex) when (owningOperationId != null)
                {
                    using var clean = scopeFactory.CreateScope(); var service = clean.ServiceProvider.GetRequiredService<MigrationCentreService>();
                    var childDb = clean.ServiceProvider.GetRequiredService<IErpDbContext>();
                    var modules = group.Select(x => x.Module).ToArray(); var code = group.First().Scope;
                    var items = await childDb.MigrationOperationItems.Where(x => x.OperationId == owningOperationId && modules.Contains(x.Module) && x.Code == code && x.Status == "Pending").Select(x => x.Id).ToListAsync(ct);
                    foreach (var item in items) await service.CompleteOperationItemAsync(owningOperationId.Value, item, ex.Message, ct);
                }
            }
            var savedRun = await db.MigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId, ct); run.CommittedUnits = savedRun.CommittedUnits;
            var issues = owningOperationId != null && await db.MigrationOperationItems.AnyAsync(x => x.OperationId == owningOperationId && x.Status == "NeedsAttention", ct);
            run.Status = issues ? "CompletedWithIssues" : "Completed"; run.CompletedAt = DateTime.UtcNow; Audit(p.Id, "Execution completed", $"Run {runId}; {run.CommittedUnits} selected units committed."); await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // A rollback can leave tracked entities mutated. Persist failure using a fresh job scope,
            // never SaveChanges the failed transaction's change tracker.
            throw new MigrationRunException(runId, ex.Message, ex);
        }
    }
    private async Task ExecutePostingGroupAsync(int runId, List<MigrationPreviewUnit> group, CancellationToken ct)
    {
        var run = await db.MigrationRuns.SingleAsync(x => x.Id == runId, ct);
        using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.LockMigrationProjectAsync(run.ProjectId, ct);
        var p = await Project(run.ProjectId, true, ct); await Editable(p, ct);
        using var actor = db.EnterMigrationActor(ActorId); using var permission = db.EnterMigrationPosting(p.Id);
        var modules = group.Select(x => x.Module).Distinct().ToList(); var code = group[0].Scope;
        if (owningOperationId != null && !await db.MigrationOperationItems.AnyAsync(x => x.OperationId == owningOperationId && modules.Contains(x.Module) && x.Code == code && x.Status == "Pending", ct)) return;
        var selection = new MigrationSelection { Modules = modules };
        switch (MigrationCatalog.ScopeKind(group[0].Module)) { case "Customer": selection.Customers.Add(code); break; case "Supplier": selection.Suppliers.Add(code); break; case "Stock": selection.Skus.Add(code); break; case "Bank": selection.Banks.Add(code); break; }
        var fresh = await ValidateAsync(p.Id, selection, ct);
        if (fresh.Errors.Count != 0) throw new InvalidOperationException(string.Join("; ", fresh.Errors.Select(x => x.Message)));
        var settings = MigrationJson.Read<MigrationSettings>(p.SettingsJson);
        foreach (var expected in group)
        {
            var checkedUnit = fresh.Units.SingleOrDefault(x => x.Module == expected.Module && x.Scope == expected.Scope);
            if (checkedUnit == null || checkedUnit.Hash != expected.Hash || checkedUnit.Errors.Count != 0) throw new InvalidOperationException($"{expected.Module}/{expected.Scope}: mappings, data, accounting period or financial activity changed after approval. Review this scope again.");
        }
        foreach (var u in group.OrderBy(x => Array.IndexOf(MigrationCatalog.Modules.Keys.ToArray(), x.Module)))
        {
            var old = await db.MigrationUnits.SingleOrDefaultAsync(x => x.ProjectId == p.Id && x.Module == u.Module && x.ScopeKey == u.Scope && x.Status == "Posted", ct);
            if (old != null) { if (old.Hash != u.Hash) throw new InvalidOperationException("Previously committed scope differs from the approved run."); }
            else { await PostUnit(p, u, settings, ct); run.CommittedUnits++; }
            if (owningOperationId is { } operationId)
            {
                var item = await db.MigrationOperationItems.SingleAsync(x => x.OperationId == operationId && x.Module == u.Module && x.Code == u.Scope, ct);
                await CompleteOperationItemAsync(operationId, item.Id, null, ct);
                item.Message = "Imported";
                var op = await db.MigrationOperations.SingleAsync(x => x.Id == operationId, ct); op.CurrentItem = u.Module + "/" + u.Scope; op.Phase = "Importing selected units";
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task<MigrationRunPayload> ReadRunPayloadAsync(MigrationRun run, CancellationToken ct) => MigrationJson.Read<MigrationRunPayload>(
        await db.MigrationRunPayloads.AsNoTracking().Where(x => x.RunId == run.Id).Select(x => x.PayloadJson).SingleOrDefaultAsync(ct) ?? run.SelectionJson);
    public async Task MarkRunFailedAsync(int runId, string error, CancellationToken ct)
    {
        var run = await db.MigrationRuns.SingleAsync(x => x.Id == runId, ct); if (run.Status == "Completed") return;
        run.Status = "Failed"; run.Error = error.Length > 4000 ? error[..4000] : error;
        var expected = (await ReadRunPayloadAsync(run, ct)).Preview.Units.Where(x => x.Status == "Pending").Select(x => (x.Module, x.Scope, x.Hash)).ToHashSet();
        var modules = expected.Select(x => x.Module).Distinct().ToArray();
        var units = await db.MigrationUnits.AsNoTracking().Where(x => x.ProjectId == run.ProjectId && x.Status == "Posted" && modules.Contains(x.Module)).Select(x => new { x.Module, x.ScopeKey, x.Hash }).ToListAsync(ct);
        run.CommittedUnits = units.Count(x => expected.Contains((x.Module, x.ScopeKey, x.Hash)));
        Audit(run.ProjectId, "Execution stopped", $"Run {runId}: {run.Error}. Committed work retained; uncommitted work rolled back."); await db.SaveChangesAsync(ct);
    }
    private async Task PostUnit(MigrationProject p, MigrationPreviewUnit preview, MigrationSettings settings, CancellationToken ct)
    {
        // Re-read current validation before every atomic group to catch changed periods/mappings/activity.
        if (preview.Errors.Count != 0) throw new InvalidOperationException("An invalid unit cannot be posted.");
        var unit = new MigrationUnit { ProjectId = p.Id, Module = preview.Module, ScopeKey = preview.Scope, Hash = preview.Hash, RowsJson = MigrationJson.Write(preview.Rows) };
        db.MigrationUnits.Add(unit); await db.SaveChangesAsync(ct);
        CustomerMigrationBatch? batch = null;
        if (preview.Module is "CustomerOpenings" or "ReceivedCheques")
        {
            batch = new CustomerMigrationBatch { CutoverDate = p.OpeningDate, SourceAsOfDate = p.ClosingDate, Status = "Posted", PostedAt = DateTime.UtcNow, SourceHash = MigrationJson.Hash($"CENTRE:{p.Id}:{unit.Id}:{preview.Hash}"), ReviewHash = preview.Hash, SourceJson = unit.RowsJson, ReviewJson = "{}" };
            db.CustomerMigrationBatches.Add(batch); await db.SaveChangesAsync(ct);
        }
        foreach (var r in preview.Rows)
        {
            var identity = MigrationCatalog.Identity(preview.Module, r); var identityHash = MigrationJson.Hash(identity);
            if (await db.MigrationOrigins.AnyAsync(x => x.SourceSystem == p.SourceSystem && x.RecordKind == preview.Module && x.IdentityHash == identityHash && x.IsCurrent, ct)) throw new InvalidOperationException("A legacy identity was already imported. The unique origin prevents duplicate posting.");
            AuditableEntity? entity = null; var entityKind = "";
            int Id(string field) => int.Parse(r.Get("_" + field + "Id"));
            switch (preview.Module)
            {
                case "Accounts":
                    var account = await db.Accounts.SingleOrDefaultAsync(x => x.Code == r.Get("Code"), ct);
                    if (account == null) { account = new Account { Code = r.Get("Code"), Name = r.Get("Name"), Type = Enum.Parse<AccountType>(r.Get("AccountType")), IsTransactionAccount = true, ParentAccountId = r.Get("ParentCode") == "" ? null : await db.Accounts.Where(x => x.Code == r.Get("ParentCode")).Select(x => (int?)x.Id).SingleAsync(ct) }; db.Accounts.Add(account); }
                    entity = account; entityKind = "Account"; break;
                case "CustomerMaster":
                    var customerId = await EntityId("Customer", r.Get("Code"), settings, ct);
                    var customer = customerId == null ? null : await db.Customers.SingleAsync(x => x.Id == customerId, ct);
                    if (customer == null)
                    {
                        customer = new Customer { LegacyCustomerCode = r.Get("Code"), Name = r.Get("Name"), ContactPerson = r.Get("Contact"), Email = r.Get("Email"), Phone = r.Get("Phone"), Mobile = r.Get("Mobile"), AddressLine1 = r.Get("Address1"), AddressLine2 = r.Get("Address2"), TaxRegNo = r.Get("TaxNumber"), CustomerGroupId = Id("GroupCode"), PriceLevelId = Id("PriceLevelCode"), PaymentTermId = Id("PaymentTermCode"), DefaultReceivableAccountId = Id("ReceivableAccountCode"), DefaultRevenueAccountId = Id("RevenueAccountCode"), CreditPeriodDays = 30, DetailsIncomplete = new[] { "Email", "Contact", "Phone", "Address1", "TaxNumber" }.Any(f => r.Get(f) == ""), InternalNotes = "Migrated master; complete genuine missing details through customer maintenance." }; db.Customers.Add(customer);
                    }
                    else { if (customer.LegacyCustomerCode != null && customer.LegacyCustomerCode != r.Get("Code")) throw new InvalidOperationException("Mapped customer already has a different legacy code."); customer.LegacyCustomerCode = r.Get("Code"); }
                    entity = customer; entityKind = "Customer"; break;
                case "SupplierMaster":
                    var supplierId = await EntityId("Supplier", r.Get("Code"), settings, ct);
                    var supplier = supplierId == null ? null : await db.Suppliers.SingleAsync(x => x.Id == supplierId, ct);
                    if (supplier == null)
                    {
                        supplier = new Supplier { LegacySupplierCode = r.Get("Code"), Name = r.Get("Name"), ContactPerson = r.Get("Contact"), Email = r.Get("Email"), Phone = r.Get("Phone"), Mobile = r.Get("Mobile"), AddressLine1 = r.Get("Address1"), AddressLine2 = r.Get("Address2"), TaxRegNo = r.Get("TaxNumber"), SupplierGroupId = Id("GroupCode"), PaymentTermId = Id("PaymentTermCode"), DefaultPayableAccountId = Id("PayableAccountCode"), DefaultExpenseAccountId = Id("ExpenseAccountCode"), DetailsIncomplete = new[] { "Email", "Contact", "Phone", "Address1", "TaxNumber" }.Any(f => r.Get(f) == ""), InternalNotes = "Migrated master; complete genuine missing details through supplier maintenance." }; db.Suppliers.Add(supplier);
                    }
                    else { if (supplier.LegacySupplierCode != null && supplier.LegacySupplierCode != r.Get("Code")) throw new InvalidOperationException("Mapped supplier already has a different legacy code."); supplier.LegacySupplierCode = r.Get("Code"); }
                    entity = supplier; entityKind = "Supplier"; break;
                case "CustomerOpenings":
                    var ci = new CustomerOpeningItem { MigrationBatchId = batch!.Id, CustomerId = await EntityId("Customer", r.Get("Code"), settings, ct) ?? throw new InvalidOperationException("Customer is not imported."), LegacyCustomerCode = r.Get("Code"), ReceivableAccountId = int.Parse(r.Get("_ControlId")), ReferenceNo = r.Get("Reference"), Kind = r.Get("Kind"), DocumentDate = r.Date("DocumentDate"), PostingDate = p.OpeningDate, DueDate = r.Get("DueDate") == "" ? null : r.Date("DueDate"), Amount = r.Decimal("Amount"), SourceRow = r.Row, Notes = $"Migration Centre project {p.Id}; original legacy closing {p.ClosingDate:yyyy-MM-dd}" }; db.CustomerOpeningItems.Add(ci); entity = ci; entityKind = "CustomerOpeningItem"; break;
                case "SupplierOpenings":
                    var si = new SupplierOpeningItem { MigrationUnitId = unit.Id, SupplierId = await EntityId("Supplier", r.Get("Code"), settings, ct) ?? throw new InvalidOperationException("Supplier is not imported."), PayableAccountId = int.Parse(r.Get("_ControlId")), ReferenceNo = r.Get("Reference"), Kind = r.Get("Kind"), DocumentDate = r.Date("DocumentDate"), PostingDate = p.OpeningDate, DueDate = r.Get("DueDate") == "" ? null : r.Date("DueDate"), Amount = r.Decimal("Amount") }; db.SupplierOpeningItems.Add(si); entity = si; entityKind = "SupplierOpeningItem"; break;
                case "ReceivedCheques":
                    var cheque = new ChequeRegister { MigrationBatchId = batch!.Id, CustomerId = await EntityId("Customer", r.Get("Code"), settings, ct) ?? throw new InvalidOperationException("Customer is not imported."), ChequeNumber = r.Get("ChequeNumber"), BankBranchId = Id("BankBranchCode"), ChequeDate = r.Date("ChequeDate"), ReceivedDate = r.Date("ReceivedDate"), Amount = r.Decimal("Amount"), LegacyReceiptReference = r.Get("Reference"), LegacyDocumentReferences = r.Get("DocumentReferences"), OpeningReceivableAccountId = int.Parse(r.Get("_ControlId")), OpeningUndepositedAccountId = Id("SafeAccountCode"), OpeningSourceRow = r.Row, Status = Enum.Parse<ChequeStatus>(r.Get("Status")) };
                    if (cheque.Status == ChequeStatus.Deposited) { cheque.DepositedBankAccountId = Id("BankAccountCode"); cheque.DepositedDate = p.ClosingDate; db.OpeningBankItems.Add(new OpeningBankItem { MigrationUnitId = unit.Id, ReceivedCheque = cheque, ChequeNumber = cheque.ChequeNumber, ChequeDate = cheque.ChequeDate, BankAccountId = cheque.DepositedBankAccountId.Value, Reference = r.Get("Reference"), Kind = "ReceivedCheque", Date = cheque.ReceivedDate!.Value, Amount = cheque.Amount, LedgerAlreadyReflected = true }); }
                    if (cheque.Status == ChequeStatus.Endorsed) { cheque.OpeningEndorsedSupplierId = await EntityId("Supplier", MigrationJson.Code(r.Get("SupplierCode")), settings, ct) ?? throw new InvalidOperationException("Endorsed supplier is not mapped."); cheque.EndorsedDate = p.ClosingDate; }
                    db.ChequeRegisters.Add(cheque); entity = cheque; entityKind = "Cheque"; break;
                case "IssuedCheques":
                    var issued = new OpeningBankItem { MigrationUnitId = unit.Id, ChequeNumber = r.Get("ChequeNumber"), ChequeDate = r.Date("ChequeDate"), BankAccountId = Id("BankAccountCode"), SupplierId = await EntityId("Supplier", r.Get("Code"), settings, ct), Reference = r.Get("Reference"), Kind = "IssuedCheque", Date = r.Date("DocumentDate"), Amount = -r.Decimal("Amount"), LedgerAlreadyReflected = true }; db.OpeningBankItems.Add(issued); entity = issued; entityKind = "OpeningBankItem"; break;
                case "Stock":
                    var variantId = await EntityId("Stock", r.Get("Code"), settings, ct);
                    var variant = variantId == null ? null : await db.ProductVariants.SingleAsync(x => x.Id == variantId, ct);
                    if (variant == null)
                    {
                        var product = new Product { Name = r.Get("Name"), BrandId = Id("BrandCode"), CategoryId = Id("CategoryCode"), UnitOfMeasureId = Id("UnitCode"), Type = Enum.Parse<ProductType>(r.Get("ProductType")) }; db.Products.Add(product);
                        variant = new ProductVariant { Product = product, SKU = r.Get("Code"), Name = r.Get("Name"), Size = r.Get("Size"), Color = r.Get("Color"), CostPrice = r.Decimal("_BaseCost"), MovingAverageCost = r.Decimal("_BaseCost"), SellingPrice = r.Get("SellingPrice") == "" ? 0 : r.Decimal("SellingPrice") }; db.ProductVariants.Add(variant); await db.SaveChangesAsync(ct);
                    }
                    var layer = new StockLayer { ProductVariantId = variant.Id, WarehouseId = Id("WarehouseCode"), BatchNo = r.Get("Batch"), DateReceived = r.Date("LayerDate"), InitialQty = r.Decimal("_BaseQuantity"), RemainingQty = r.Decimal("_BaseQuantity"), UnitCost = r.Decimal("_BaseCost"), IsExhausted = r.Decimal("_BaseQuantity") == 0 }; db.StockLayers.Add(layer);
                    db.StockTransactions.Add(new StockTransaction { ProductVariantId = variant.Id, WarehouseId = layer.WarehouseId, Date = p.OpeningDate, Type = StockTransactionType.OpeningBalance, Qty = layer.InitialQty, UnitCost = layer.UnitCost, TotalValue = decimal.Round(layer.InitialQty * layer.UnitCost, 2, MidpointRounding.AwayFromZero), ReferenceDocNo = $"MIG-{p.Id}-{unit.Id}", Notes = "Legacy opening; original FIFO layer date preserved." });
                    entity = layer; entityKind = "StockLayer"; break;
                case "BankOpening": break;
                case "BankRecon":
                    var bankItem = new OpeningBankItem { MigrationUnitId = unit.Id, BankAccountId = Id("BankAccountCode"), Reference = r.Get("Reference"), Date = r.Date("DocumentDate"), Amount = r.Decimal("Amount"), Kind = r.Get("Kind"), LedgerAlreadyReflected = true }; db.OpeningBankItems.Add(bankItem); entity = bankItem; entityKind = "OpeningBankItem"; break;
                case "TrialBalance": break;
            }
            await db.SaveChangesAsync(ct);
            db.MigrationOrigins.Add(new MigrationOrigin { ProjectId = p.Id, UnitId = unit.Id, SourceSystem = p.SourceSystem, RecordKind = preview.Module, IdentityHash = identityHash, ContentHash = MigrationJson.Hash(r.Canonical), LegacyRecordId = identity, EntityKind = entityKind == "" ? "MigrationUnit" : entityKind, EntityId = entity?.Id ?? unit.Id });
        }
        if (preview.Journal.Count != 0)
        {
            var journal = await journals.PostJournalAsync(new JournalEntryRequest { Date = p.OpeningDate, Module = "MigrationCentre", ReferenceNo = $"MIG-{p.Id}-{unit.Id}", Description = $"Migration opening {preview.Module}/{preview.Scope}", Lines = preview.Journal.Select(x => new JournalLineRequest { AccountId = x.AccountId, Debit = x.Debit, Credit = x.Credit }).ToList() });
            if (!journal.Succeeded) throw new InvalidOperationException(journal.Message); unit.JournalEntryId = journal.Data; if (batch != null) batch.JournalEntryId = journal.Data;
        }
        if (preview.Module == "Stock")
        {
            var variantId = await EntityId("Stock", preview.Scope, settings, ct);
            var variant = await db.ProductVariants.SingleAsync(x => x.Id == variantId, ct);
            var layers = await db.StockLayers.Where(x => x.ProductVariantId == variant.Id && x.RemainingQty > 0).ToListAsync(ct);
            var quantity = layers.Sum(x => x.RemainingQty);
            if (quantity > 0) variant.MovingAverageCost = decimal.Round(layers.Sum(x => x.RemainingQty * x.UnitCost) / quantity, 6, MidpointRounding.AwayFromZero);
        }
        var kind = MigrationCatalog.ScopeKind(preview.Module);
        var scope = await db.MigrationScopes.SingleOrDefaultAsync(x => x.ProjectId == p.Id && x.Kind == kind && x.Code == preview.Scope, ct);
        if (scope != null) scope.EntityId = await EntityId(kind, scope.Code, settings, ct);
        Audit(p.Id, "Unit posted", $"{preview.Module}/{preview.Scope}; unit {unit.Id}; journal {unit.JournalEntryId}; hash {unit.Hash}."); await db.SaveChangesAsync(ct);
    }
}
public sealed class MigrationRunPayload
{
    public MigrationSelection Selection { get; set; } = new();
    public MigrationPreview Preview { get; set; } = new();
}
public sealed class MigrationRunException(int runId, string message, Exception inner) : Exception(message, inner)
{
    public int RunId { get; } = runId;
}
