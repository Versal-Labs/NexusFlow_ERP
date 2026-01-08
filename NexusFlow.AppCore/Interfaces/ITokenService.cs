using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(string userId, string email, IList<string> roles);
    }
}
