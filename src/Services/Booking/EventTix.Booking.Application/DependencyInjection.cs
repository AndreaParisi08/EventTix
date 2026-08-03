using EventTix.Booking.Application.Bookings.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventTix.Booking.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            var assembly = typeof(DependencyInjection).Assembly;

            // 1. Registrazione di MediatR e della Pipeline di Validazione
            services.AddMediatR(x =>
            {
                x.RegisterServicesFromAssembly(assembly);
                x.AddOpenBehavior(typeof(IdempotentCommandBehavior<,>));
            });

            // 2. Registrazione automatica di tutti i FluentValidators presenti nel progetto
            services.AddValidatorsFromAssembly(assembly);

            return services;
        }
    }
}