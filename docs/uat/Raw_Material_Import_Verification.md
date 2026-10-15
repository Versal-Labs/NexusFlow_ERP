# Raw Material Import Verification

## Local HTTP harness

`tools/RawMaterialImportSmokeHost` runs the real compiled Razor views, API controller, MediatR handler, stock writes and journal service against a uniquely named disposable SQL Server LocalDB database. It does not start the production application, read installation secrets, or use its connection strings. Test authentication is synthetic and exists only in this separate opt-in project. Do not deploy this host.

From the repository root on Windows with `MSSQLLocalDB` installed:

```powershell
$env:NEXUSFLOW_RUN_LOCALDB_IMPORT_TESTS='1'
dotnet run --project tools/RawMaterialImportSmokeHost/RawMaterialImportSmokeHost.csproj --no-launch-profile
```

In another terminal:

```powershell
./tools/RawMaterialImportSmokeHost/verify.ps1
```

The script verifies actual Razor output, static JS, CSRF rejection, setup retrieval, read-only preview, explicit confirmation, stale preview rejection, two-row synthetic posting, ledger/journal counts, repeat rejection and read-only access denial. Optional `-SourceCsv <absolute-path>` previews another CSV locally without posting it. The supplied CSV is not copied into the repository.

For manual visual checks, open `http://127.0.0.1:5189/smoke-login`, choose the synthetic fixture, preview and inspect the page at desktop/mobile widths. Restart with a fresh disposable database before running `verify.ps1` if those SKUs were already posted manually.

Graceful shutdown removes the synthetic database:

```powershell
Invoke-RestMethod -Method Post http://127.0.0.1:5189/smoke-stop
```

Do not terminate the process forcibly; it may prevent cleanup. Database names begin with `NexusFlow_RawImport_Test_` and end with a generated GUID. Never run cleanup against a configured application database.

## Verification status

- Build passed with pre-existing warnings, including a package advisory for `Microsoft.OpenApi` 2.0.0; dependency remediation is outside this import change.
- Parser/handler tests: 23 passed.
- Final full integration regression suite with LocalDB enabled: 57 passed, 0 failed, 0 skipped.
- Real SQL Server tests: 3 passed, including rollback after a forced journal failure, correct persisted quantities/costs/journal balances, and concurrent same-file posting committing only one import.
- JavaScript state tests: 4 passed (defaults, safe text rendering, invalidation, confirmation, busy lock, duplicate clicks and failure recovery).
- HTTP smoke checks passed; only the synthetic fixture was posted locally.
- Corrected source CSV preview: 444 rows; 6,306,283 PCS; 29,727,743.70 opening value. No real opening stock was posted.
- Browser tool initialization failed twice. Visual and hosted UAT remain pending; no screenshots or browser passes are claimed.
- No deployment, database migration or SQL insert script was performed.
