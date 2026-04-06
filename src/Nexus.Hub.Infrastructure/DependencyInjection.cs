using Microsoft.Extensions.DependencyInjection;
using Nexus.Hub.Domain.Repositories;
using Nexus.Hub.Domain.Services;
using Nexus.Hub.Infrastructure.Repositories;
using Nexus.Hub.Infrastructure.Services;

namespace Nexus.Hub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<ISpokeRepository, SpokeRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IOutputStreamRepository, OutputStreamRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();

        // Services
        services.AddScoped<ISpokeService, SpokeService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IConversationService, ConversationService>();

        return services;
    }
}
