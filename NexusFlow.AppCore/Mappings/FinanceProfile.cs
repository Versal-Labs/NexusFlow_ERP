using AutoMapper;
using NexusFlow.AppCore.DTOs.Finance;
using NexusFlow.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace NexusFlow.AppCore.Mappings
{
    public class FinanceProfile : Profile
    {
        public FinanceProfile()
        {
            CreateMap<Account, AccountDto>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.Children, opt => opt.Ignore()); // Handled manually
        }
    }
}
