// <copyright file="Startup.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace devdays_2019_subscription_cs
{
    /// <summary>A startup.</summary>
    public class Startup
    {
        /// <summary>Configure services.</summary>
        /// <param name="services">The services.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            // add MVC to our services
            services.AddMvc();

            // because we are loading the web host manually, we need to force it to load our local assemblies
            services.AddMvc().AddApplicationPart(Assembly.GetExecutingAssembly());

            // inject the configuration singleton into our services
            services.AddSingleton<IConfiguration>(Program.Configuration);
        }

        /// <summary>Configures.</summary>
        /// <param name="app">The application.</param>
        /// <param name="env">The environment.</param>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // we want to essentially disable CORS
            app.UseCors(builder => builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                //.AllowCredentials()
                );

            // flag we want MVC (for API routing, Controllers are decorated with routes)
            app.UseMvc();
        }
    }
}
