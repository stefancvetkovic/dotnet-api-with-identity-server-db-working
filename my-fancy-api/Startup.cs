using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.OpenApi.Models;
using my_fancy_api.Data;
using my_fancy_api.Swagger;

namespace my_fancy_api
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
            var connectionString = Configuration.GetConnectionString("IdentityServerDatabase");
            var migrationAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            services.AddControllers();
            services.AddLocalApiAuthentication();

            services.AddIdentityServer()
                    .AddDeveloperSigningCredential()
                    .AddConfigurationStore(options =>
                    {
                        options.ConfigureDbContext = b => b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationAssembly));
                    })
                    .AddOperationalStore(options =>
                    {
                        options.ConfigureDbContext = b => b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationAssembly));
                    });

            //services.AddAuthentication("Bearer")
            //        .AddIdentityServerAuthentication(options =>
            //        {
            //            options.Authority = "https://localhost:44306";
            //            options.RequireHttpsMetadata = false;
            //            options.ApiName = "my-api";
            //        });

            services.AddAuthorization();

            services.AddSwaggerGen(options =>
            {
                options.OperationFilter<SwaggerAuthenticationRequirementsOperationFilter>();

                options.AddSecurityDefinition("My Security Definition", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    OpenIdConnectUrl = new Uri($"https://localhost:5001/.well-known/openid-configuration"),
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri($"https://localhost:5001/connnect/authorize"),
                            TokenUrl = new Uri($"https://localhost:5001/connect/token"),
                            Scopes = new Dictionary<string, string>
                                {
                                    { "write", "the right to write" },
                                    { "read", "the right to read" }
                                }
                        }
                    }
                });

                var filePath = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "my-fancy-api.xml");

                options.IncludeXmlComments(filePath);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            DatabaseInitializer.PopulateIdentityServer(app);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSwagger();

            app.UseSwaggerUI(options => {
                options.SwaggerEndpoint($"/swagger/v1/swagger.json", "V1");
            });

            app.UseIdentityServer();
        }
    }
}
