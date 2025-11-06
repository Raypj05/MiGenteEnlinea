using AutoMapper;
using MiGenteEnLinea.Application.Features.Suscripciones.DTOs;
using MiGenteEnLinea.Domain.Entities.Suscripciones;

namespace MiGenteEnLinea.Application.Features.Suscripciones.Mappings;

/// <summary>
/// Perfil de AutoMapper para mapear Suscripcion a SuscripcionDto.
/// </summary>
public class SuscripcionMappingProfile : Profile
{
    public SuscripcionMappingProfile()
    {
        CreateMap<Suscripcion, SuscripcionDto>()
            .ForMember(dest => dest.EstaActiva, opt => opt.MapFrom(src => src.EstaActiva()))
            .ForMember(dest => dest.DiasRestantes, opt => opt.MapFrom(src => src.DiasRestantes()));

        CreateMap<PlanEmpleador, PlanDto>()
            .ForMember(dest => dest.TipoPlan, opt => opt.MapFrom(src => "Empleador"))
            .ForMember(dest => dest.LimiteEmpleados, opt => opt.MapFrom(src => src.LimiteEmpleados))
            .ForMember(dest => dest.MesesHistorico, opt => opt.MapFrom(src => src.MesesHistorico))
            .ForMember(dest => dest.IncluyeNomina, opt => opt.MapFrom(src => src.IncluyeNomina));

        CreateMap<PlanContratista, PlanDto>()
            .ForMember(dest => dest.Nombre, opt => opt.MapFrom(src => src.NombrePlan))
            .ForMember(dest => dest.TipoPlan, opt => opt.MapFrom(src => "Contratista"))
            .ForMember(dest => dest.LimiteEmpleados, opt => opt.MapFrom(src => (int?)null))
            .ForMember(dest => dest.MesesHistorico, opt => opt.MapFrom(src => (int?)null))
            .ForMember(dest => dest.IncluyeNomina, opt => opt.MapFrom(src => (bool?)null));
    }
}
