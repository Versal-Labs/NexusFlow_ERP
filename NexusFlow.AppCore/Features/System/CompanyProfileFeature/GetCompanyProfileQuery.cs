using MediatR;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Shared.Wrapper;
using System.Threading;
using System.Threading.Tasks;
using NexusFlow.AppCore.Interfaces;

namespace NexusFlow.AppCore.Features.System.CompanyProfileFeature
{
    public class GetCompanyProfileQuery : IRequest<Result<CompanyProfile>>
    {
    }

    public class GetCompanyProfileQueryHandler : IRequestHandler<GetCompanyProfileQuery, Result<CompanyProfile>>
    {
        private readonly ICompanyProfileService _companyProfileService;

        public GetCompanyProfileQueryHandler(ICompanyProfileService companyProfileService)
        {
            _companyProfileService = companyProfileService;
        }

        public async Task<Result<CompanyProfile>> Handle(GetCompanyProfileQuery request, CancellationToken cancellationToken)
        {
            var profile = await _companyProfileService.GetProfileAsync(cancellationToken);
            return Result<CompanyProfile>.Success(profile);
        }
    }
}
