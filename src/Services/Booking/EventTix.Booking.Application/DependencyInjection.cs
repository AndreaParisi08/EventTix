using EventTix.BuildingBlocks.Application.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventTix.Booking.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            var assembly = typeof(DependencyInjection).Assembly;

            // 1. Register FluentValidation Validators from this assembly
            services.AddValidatorsFromAssembly(assembly);

            // 2. Register MediatR & Pipeline Behaviors in order
            services.AddMediatR(x =>
            {
                x.RegisterServicesFromAssembly(assembly);

                // Step 1: Validate incoming command payload
                x.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));

                // Step 2: Intercept idempotent requests via Redis
                x.AddOpenBehavior(typeof(IdempotentCommandBehavior<,>));
            });

            

            return services;
        }
    }
}