using FluentAssertions;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Installation;
using NexusFlow.Infrastructure.Installation;

namespace NexusFlow.IntegrationTests
{
    public sealed class InstallationFoundationTests
    {
        [Fact]
        public async Task Setup_key_is_one_time_and_never_persisted_in_clear_text()
        {
            using var instance = new TemporaryInstallationInstance();
            var store = new JsonInstallationStateStore(instance.Paths);

            await store.EnsureInitializedAsync("one-time-secret");

            store.VerifySetupKey("wrong-key").Should().BeFalse();
            store.VerifySetupKey("one-time-secret").Should().BeTrue();
            File.ReadAllText(instance.Paths.StateFilePath).Should().NotContain("one-time-secret");

            await store.ConsumeSetupKeyAsync();

            store.VerifySetupKey("one-time-secret").Should().BeFalse();
            store.Get().SetupKeyConsumed.Should().BeTrue();
        }

        [Fact]
        public void Legacy_PowerShell_timestamp_does_not_make_installation_state_unreadable()
        {
            using var instance = new TemporaryInstallationInstance();
            instance.Paths.EnsureDirectories();
            File.WriteAllText(instance.Paths.StateFilePath, """
                {
                  "InstanceId": "test-instance",
                  "Mode": 0,
                  "UpdatedAtUtc": "/Date(1781271326278)/"
                }
                """);

            var state = new JsonInstallationStateStore(instance.Paths).Get();

            state.Mode.Should().Be(ApplicationMode.Uninitialized);
            state.LastError.Should().BeNull();
            state.UpdatedAtUtc.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1781271326278));
        }

        [Fact]
        public async Task Dpapi_secret_store_does_not_persist_clear_text()
        {
            if (!OperatingSystem.IsWindows()) return;

            using var instance = new TemporaryInstallationInstance();
            var store = new DpapiInstallationSecretStore(instance.Paths);

            await store.SetAsync("Database", "Server=sql;Password=very-secret;");

            store.Get("Database").Should().Be("Server=sql;Password=very-secret;");
            Convert.ToBase64String(File.ReadAllBytes(instance.Paths.SecretFilePath))
                .Should().NotContain("very-secret");
        }

        [Fact]
        public void Required_catalogs_are_unique_and_role_manifest_has_protected_superadmin()
        {
            ConfigurationKeys.Required.Should().OnlyHaveUniqueItems();
            AccountMappingKeys.Required.Should().OnlyHaveUniqueItems();
            NumberSequenceKeys.Required.Should().OnlyHaveUniqueItems();
            NumberSequenceKeys.Required.Should().Contain([
                NumberSequenceKeys.CreditNote,
                NumberSequenceKeys.DebitNote,
                NumberSequenceKeys.Employee,
                NumberSequenceKeys.GoodsReceipt,
                NumberSequenceKeys.Journal,
                NumberSequenceKeys.MaterialIssue,
                NumberSequenceKeys.SalesOrder,
                NumberSequenceKeys.Payment,
                NumberSequenceKeys.ProductionReceipt,
                NumberSequenceKeys.Purchasing,
                NumberSequenceKeys.Receipt,
                NumberSequenceKeys.SalesInvoice,
                NumberSequenceKeys.StockAdjustment,
                NumberSequenceKeys.StockTake,
                NumberSequenceKeys.StockTransfer,
                NumberSequenceKeys.SupplierBill
            ]);
            DefaultRoleManifest.Roles[DefaultRoleManifest.SuperAdmin]
                .Should().ContainSingle(x => x == Permissions.SuperAdmin);
        }

        private sealed class TemporaryInstallationInstance : IDisposable
        {
            private readonly string? _previousRoot;
            private readonly string? _previousId;

            public TemporaryInstallationInstance()
            {
                _previousRoot = Environment.GetEnvironmentVariable("NEXUSFLOW_INSTANCE_ROOT");
                _previousId = Environment.GetEnvironmentVariable("NEXUSFLOW_INSTANCE_ID");
                Root = Path.Combine(Path.GetTempPath(), "NexusFlow.Tests", Guid.NewGuid().ToString("N"));
                Environment.SetEnvironmentVariable("NEXUSFLOW_INSTANCE_ROOT", Root);
                Environment.SetEnvironmentVariable("NEXUSFLOW_INSTANCE_ID", "test-instance");
                Paths = new InstallationPaths();
            }

            public string Root { get; }
            public InstallationPaths Paths { get; }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable("NEXUSFLOW_INSTANCE_ROOT", _previousRoot);
                Environment.SetEnvironmentVariable("NEXUSFLOW_INSTANCE_ID", _previousId);
                if (Directory.Exists(Root))
                    Directory.Delete(Root, true);
            }
        }
    }
}
