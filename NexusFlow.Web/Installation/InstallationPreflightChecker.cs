using System.Security.Cryptography;
using NexusFlow.AppCore.Installation;
using NexusFlow.Infrastructure.Installation;

namespace NexusFlow.Web.Installation
{
    public interface IInstallationPreflightChecker
    {
        IReadOnlyList<ReadinessCheck> Check(HttpRequest request);
    }

    public sealed class InstallationPreflightChecker : IInstallationPreflightChecker
    {
        private readonly InstallationPaths _paths;

        public InstallationPreflightChecker(InstallationPaths paths)
        {
            _paths = paths;
        }

        public IReadOnlyList<ReadinessCheck> Check(HttpRequest request)
        {
            var checks = new List<ReadinessCheck>
            {
                new("preflight.https", "Installer is accessed over HTTPS", request.IsHttps,
                    request.IsHttps ? null : "Configure an IIS HTTPS binding before installation."),
                new("preflight.windows", "Windows hosting environment is available", OperatingSystem.IsWindows(),
                    OperatingSystem.IsWindows() ? null : "DPAPI secret protection requires Windows."),
                CheckWritableDirectory(_paths.RootPath),
                CheckDiskSpace(_paths.RootPath),
                CheckDpapi()
            };

            return checks;
        }

        private static ReadinessCheck CheckWritableDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                var probe = Path.Combine(path, $".preflight-{Guid.NewGuid():N}");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return new("preflight.directories", "Instance directories are writable", true);
            }
            catch (Exception ex)
            {
                return new("preflight.directories", "Instance directories are writable", false, ex.Message);
            }
        }

        private static ReadinessCheck CheckDiskSpace(string path)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(path))!;
                var freeBytes = new DriveInfo(root).AvailableFreeSpace;
                var passed = freeBytes >= 1024L * 1024 * 1024;
                return new("preflight.disk", "At least 1 GB free disk space is available", passed,
                    passed ? null : $"{freeBytes / 1024 / 1024:N0} MB is available.");
            }
            catch (Exception ex)
            {
                return new("preflight.disk", "At least 1 GB free disk space is available", false, ex.Message);
            }
        }

        private static ReadinessCheck CheckDpapi()
        {
            if (!OperatingSystem.IsWindows())
                return new("preflight.dpapi", "DPAPI encryption works for the app-pool identity", false, "Windows is required.");

            try
            {
                var clear = RandomNumberGenerator.GetBytes(32);
                var encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return new("preflight.dpapi", "DPAPI encryption works for the app-pool identity",
                    CryptographicOperations.FixedTimeEquals(clear, decrypted));
            }
            catch (Exception ex)
            {
                return new("preflight.dpapi", "DPAPI encryption works for the app-pool identity", false, ex.Message);
            }
        }
    }
}
