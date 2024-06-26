﻿using Microsoft.Extensions.DependencyInjection;
using services.abstractions.Interfaces;
using services.Implementations;

namespace services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IKitsService, KitsService>();
        services.AddScoped<ICategoriesService, CategoriesService>();
        services.AddScoped<IOrdersService, OrdersService>();
        return services;
    }
}