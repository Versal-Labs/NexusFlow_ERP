using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Configs.Queries
{
    public class GetCompanyNameQuery : IRequest<string> { }

    public class GetCompanyNameHandler : IRequestHandler<GetCompanyNameQuery, string>
    {
        private readonly IErpDbContext _context;
        private readonly ICompanyProfileService _companyProfileService;

        public GetCompanyNameHandler(IErpDbContext context, ICompanyProfileService companyProfileService)
        {
            _context = context;
            _companyProfileService = companyProfileService;
        }

        public async Task<string> Handle(GetCompanyNameQuery request, CancellationToken cancellationToken)
        {
            var profile = await _companyProfileService.GetProfileAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(profile.CompanyName))
                return profile.CompanyName;

            var config = await _context.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Key == "Company.Name", cancellationToken);

            return config?.Value ?? "NexusFlow Enterprise"; // Fallback name
        }
    }
}
