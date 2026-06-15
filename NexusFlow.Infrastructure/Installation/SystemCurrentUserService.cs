using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.Infrastructure.Installation
{
    public sealed class SystemCurrentUserService : ICurrentUserService
    {
        public string UserId => "SYSTEM";
        public string UserName => "SYSTEM";
        public bool IsAuthenticated => false;
        public int? EmployeeId => null;
        public bool HasPermission(string permission) => false;
    }
}
