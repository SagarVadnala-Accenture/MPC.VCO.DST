using MPC.VCO.DST.Application.Implementation;
using MPC.VCO.DST.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MPC.VCO.DST.Application;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<
            IPlanningCycleService,
            PlanningCycleService>();

        services.AddScoped<
            IModelService,
            ModelService>();

        services.AddScoped<
            IModelNoteService,
            ModelNoteService>();

        services.AddScoped<
            IMentionUserService,
            MentionUserService>();
        services.AddScoped<IOptimizerService, OptimizerService>();

        return services;
    }
}