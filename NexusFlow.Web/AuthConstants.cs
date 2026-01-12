namespace NexusFlow.Web
{
    public static class AuthConstants
    {
        // We define them as true CONSTants here
        public const string IdentityScheme = "Identity.Application";
        public const string JwtScheme = "Bearer";

        // Now this works because both parts are const
        public const string HybridScheme = IdentityScheme + "," + JwtScheme;
    }
}
