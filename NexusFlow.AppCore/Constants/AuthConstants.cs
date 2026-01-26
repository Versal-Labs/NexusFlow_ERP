using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Constants
{
    public static class AuthConstants
    {
        // The specific schemes
        public const string IdentityScheme = "Identity.Application";
        public const string JwtScheme = "Bearer";

        // The Hybrid Scheme: "Try Cookie first, then try JWT"
        // Use this for Hubs and APIs that serve both Mobile and Web
        public const string HybridScheme = IdentityScheme + "," + JwtScheme;

        // The Policy Name (Must match what is in Program.cs)
        public const string HybridPolicy = "HybridPolicy";
    }
}
