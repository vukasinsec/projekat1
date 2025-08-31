using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Neo4jClient;

namespace app
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();  // Use AddControllersWithViews for MVC with Razor views
            services.AddRazorPages(); // If you're using Razor Pages
            services.AddScoped<PostController, PostController>();
            services.AddScoped<PostController>();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "app", Version = "v1" });
            });

            var client = InitializeNeo4jClient().Result;
            services.AddSingleton<IGraphClient>(client);
            //autentifikacija
            services.AddAuthentication("Cookies")
                                .AddCookie(options =>
                                {
                                    options.LoginPath = "/Account/Login";   // Stranica za prijavu
                                    options.LogoutPath = "/Account/Logout"; // Stranica za odjavu
                                    options.AccessDeniedPath = "/Account/AccessDenied"; // Opcionalno, za pristup zabranjen
                                });

        }


        private async Task<IGraphClient> InitializeNeo4jClient()
{
    var client = new BoltGraphClient("bolt://localhost:7687", "neo4j", "password");
    await client.ConnectAsync();
    return client;
}


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "app v1"));
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseCors();  // Make sure CORS is applied

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseStaticFiles();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }

    }
}