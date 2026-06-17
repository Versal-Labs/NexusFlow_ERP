using MediatR;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Shared.Wrapper;
using System.Threading;
using System.Threading.Tasks;
using NexusFlow.AppCore.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace NexusFlow.AppCore.Features.System.CompanyProfileFeature
{
    public class UpdateCompanyProfileCommand : IRequest<Result<string>>
    {
        public string CompanyName { get; set; } = null!;
        public string? TaxRegistrationNumber { get; set; }
        public string? BusinessRegistrationNumber { get; set; }
        public string? PrimaryAddress { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        
        // Upload Logo Properties
        public Stream? LogoStream { get; set; }
        public string? LogoFileName { get; set; }
        public string? LogoContentType { get; set; }
    }

    public class UpdateCompanyProfileCommandHandler : IRequestHandler<UpdateCompanyProfileCommand, Result<string>>
    {
        private readonly IErpDbContext _dbContext;
        private readonly ICompanyProfileService _companyProfileService;
        private readonly IGlobalStorageCoordinator _storageCoordinator;

        public UpdateCompanyProfileCommandHandler(
            IErpDbContext dbContext,
            ICompanyProfileService companyProfileService,
            IGlobalStorageCoordinator storageCoordinator)
        {
            _dbContext = dbContext;
            _companyProfileService = companyProfileService;
            _storageCoordinator = storageCoordinator;
        }

        public async Task<Result<string>> Handle(UpdateCompanyProfileCommand request, CancellationToken cancellationToken)
        {
            var profile = await _dbContext.CompanyProfiles.FirstOrDefaultAsync(cancellationToken);
            if (profile == null)
            {
                profile = new CompanyProfile();
                _dbContext.CompanyProfiles.Add(profile);
            }

            profile.CompanyName = request.CompanyName;
            profile.TaxRegistrationNumber = request.TaxRegistrationNumber;
            profile.BusinessRegistrationNumber = request.BusinessRegistrationNumber;
            profile.PrimaryAddress = request.PrimaryAddress;
            profile.ContactEmail = request.ContactEmail;
            profile.ContactPhone = request.ContactPhone;

            if (request.LogoStream != null && !string.IsNullOrWhiteSpace(request.LogoFileName))
            {
                if (!string.IsNullOrWhiteSpace(profile.LogoBlobUrl))
                {
                    try
                    {
                        await _storageCoordinator.DeleteFileAsync(profile.LogoBlobUrl, cancellationToken);
                    }
                    catch
                    {
                        // Ignore if old file doesn't exist
                    }
                }

                // Installation ID logic is handled by container name or storage paths behind the coordinator
                var blobUrl = await _storageCoordinator.SaveFileSecurelyAsync(
                    request.LogoStream, 
                    request.LogoFileName, 
                    "branding", 
                    request.LogoContentType ?? "image/png", 
                    cancellationToken);
                
                profile.LogoBlobUrl = blobUrl;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _companyProfileService.ClearCacheAsync(cancellationToken);

            return Result<string>.Success("Company Profile updated successfully");
        }
    }
}
