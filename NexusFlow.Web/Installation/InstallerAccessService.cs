using Microsoft.AspNetCore.DataProtection;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Infrastructure.Installation;

namespace NexusFlow.Web.Installation
{
    public interface IInstallerAccessService
    {
        bool HasAccess(HttpContext context);
        bool TryUnlock(HttpContext context, string setupKey);
        void Clear(HttpContext context);
    }

    public sealed class InstallerAccessService : IInstallerAccessService
    {
        private readonly string _cookieName;
        private readonly IDataProtector _protector;
        private readonly IInstallationStateStore _stateStore;

        public InstallerAccessService(
            IDataProtectionProvider dataProtectionProvider,
            IInstallationStateStore stateStore,
            InstallationPaths paths)
        {
            _protector = dataProtectionProvider.CreateProtector("NexusFlow.SetupAccess.v1");
            _stateStore = stateStore;
            _cookieName = $"NexusFlow.{paths.InstanceId}.Setup";
        }

        public bool HasAccess(HttpContext context)
        {
            if (!context.Request.Cookies.TryGetValue(_cookieName, out var protectedValue))
            {
                return false;
            }

            try
            {
                var parts = _protector.Unprotect(protectedValue).Split('|');
                return parts.Length == 2 &&
                       parts[0] == _stateStore.Get().InstanceId &&
                       long.TryParse(parts[1], out var expiresTicks) &&
                       DateTimeOffset.UtcNow < new DateTimeOffset(expiresTicks, TimeSpan.Zero);
            }
            catch
            {
                return false;
            }
        }

        public bool TryUnlock(HttpContext context, string setupKey)
        {
            if (!_stateStore.VerifySetupKey(setupKey))
            {
                return false;
            }

            var expires = DateTimeOffset.UtcNow.AddHours(2);
            var token = _protector.Protect($"{_stateStore.Get().InstanceId}|{expires.UtcTicks}");
            context.Response.Cookies.Append(_cookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                Expires = expires
            });
            return true;
        }

        public void Clear(HttpContext context) => context.Response.Cookies.Delete(_cookieName);
    }
}
