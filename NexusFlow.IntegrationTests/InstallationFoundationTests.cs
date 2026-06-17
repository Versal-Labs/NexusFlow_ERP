using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;
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
        public async Task Encrypted_file_secret_store_round_trips_without_clear_text()
        {
            using var instance = new TemporaryInstallationInstance();
            var store = new EncryptedFileInstallationSecretStore(instance.Paths);

            await store.SetAsync(InstallationConnectionStringProvider.DefaultConnectionSecret, "Server=sql;Password=very-secret;");

            store.Get(InstallationConnectionStringProvider.DefaultConnectionSecret)
                .Should().Be("Server=sql;Password=very-secret;");
            File.ReadAllText(instance.Paths.SecretFilePath).Should().NotContain("very-secret");
            File.Exists(instance.Paths.SecretKeyPath).Should().BeTrue();
        }

        [Fact]
        public async Task Environment_secret_store_reads_platform_connection_string_and_writes_to_fallback()
        {
            using var env = new TemporaryEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=env;");
            using var instance = new TemporaryInstallationInstance();
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var store = new CompositeInstallationSecretStore(
                new EnvironmentInstallationSecretStore(configuration),
                new EncryptedFileInstallationSecretStore(instance.Paths));

            store.Get(InstallationConnectionStringProvider.DefaultConnectionSecret).Should().Be("Server=env;");

            await store.SetAsync(InstallationConnectionStringProvider.HangfireConnectionSecret, "Server=file;");

            store.Get(InstallationConnectionStringProvider.HangfireConnectionSecret).Should().Be("Server=file;");
            File.ReadAllText(instance.Paths.SecretFilePath).Should().NotContain("Server=file;");
        }

        [Fact]
        public void Deployment_profile_defaults_choose_portable_secret_store_when_profile_is_portable()
        {
            using var profile = new TemporaryEnvironmentVariable("NEXUSFLOW_DEPLOYMENT_PROFILE", "PortableVm");
            using var instance = new TemporaryInstallationInstance();
            var configuration = new ConfigurationBuilder().Build();

            var options = InstallationRuntimeOptionsFactory.Create(configuration, instance.Paths);

            options.Profile.Should().Be(DeploymentProfile.PortableVm);
            options.SecretStoreMode.Should().Be(InstallationSecretStoreMode.EncryptedFile);
            options.StateStoreMode.Should().Be(InstallationStateStoreMode.File);
            options.DataProtectionStoreMode.Should().Be(DataProtectionStoreMode.File);
        }

        [Fact]
        public void Azure_profile_uses_cloud_defaults_when_blob_connection_is_configured()
        {
            using var profile = new TemporaryEnvironmentVariable("NEXUSFLOW_DEPLOYMENT_PROFILE", "AzureAppService");
            using var blob = new TemporaryEnvironmentVariable("ConnectionStrings__AzureBlobStorage", "UseDevelopmentStorage=true");
            using var instance = new TemporaryInstallationInstance();
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

            var options = InstallationRuntimeOptionsFactory.Create(configuration, instance.Paths);

            options.Profile.Should().Be(DeploymentProfile.AzureAppService);
            options.SecretStoreMode.Should().Be(InstallationSecretStoreMode.Environment);
            options.StateStoreMode.Should().Be(InstallationStateStoreMode.AzureBlob);
            options.DataProtectionStoreMode.Should().Be(DataProtectionStoreMode.AzureBlob);
            options.StorageMode.Should().Be(StorageMode.AzureBlob);
            options.AzureBlobStorageContainer.Should().Be("tenant-test-instance");
        }

        [Theory]
        [InlineData("Customer_A", "customer-a")]
        [InlineData("..", "tenant-default")]
        [InlineData("a", "tenant-a")]
        public void Azure_container_names_are_normalized(string input, string expected)
        {
            InstallationRuntimeOptionsFactory.NormalizeContainerName(input).Should().Be(expected);
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

        private sealed class TemporaryEnvironmentVariable : IDisposable
        {
            private readonly string _name;
            private readonly string? _previousValue;

            public TemporaryEnvironmentVariable(string name, string? value)
            {
                _name = name;
                _previousValue = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(_name, _previousValue);
            }
        }
    }
}
