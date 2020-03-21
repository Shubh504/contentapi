﻿using System.Linq;
using System.Text;
using AutoMapper;
using contentapi.Configs;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace contentapi
{

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureBasicServices(IServiceCollection services, StartupServiceConfig config)
        {
            var secretKeyBytes = Encoding.ASCII.GetBytes(config.SecretKey);

            services.AddSingleton(config.HashConfig);
            services.AddSingleton(config.EmailConfig); 
            services.AddSingleton(config.LanguageConfig); 
            services.AddSingleton(config.AccessConfig); 

            services.AddSingleton(new SessionConfig()
            {
                SecretKey = config.SecretKey
            });

            //Database config
            //services.AddDbContext<ContentDbContext>(options => options.UseLazyLoadingProxies().UseSqlite(config.ContentConString));
            services.AddDbContext<ContentDbContext>(options => options.UseSqlite(config.ContentConString).EnableSensitiveDataLogging(config.SensitiveDataLogging));

            //Mapping config
            var mapperConfig = new MapperConfiguration(cfg => 
            {
                cfg.CreateMap<UserEntity,UserView>();
                cfg.CreateMap<UserView,UserEntity>();
                cfg.CreateMap<CategoryEntity,CategoryView>();
                cfg.CreateMap<CategoryView,CategoryEntity>();
                cfg.CreateMap<ContentEntity,ContentView>();
                cfg.CreateMap<ContentView,ContentEntity>();
                cfg.CreateMap<CommentEntity,CommentView>();
                cfg.CreateMap<CommentView,CommentEntity>();
            }); 
            services.AddSingleton(mapperConfig.CreateMapper());

            //My own services (fix these to be interfaces sometime)
            services.AddTransient<PermissionService>()
                    .AddTransient<AccessService>()
                    .AddTransient<QueryService>()
                    .AddTransient<EntityControllerServices>();

            //REAL interfaced services
            services.AddTransient<IEmailService, EmailService>();
            services.AddTransient<ILanguageService, LanguageService>();
            services.AddTransient<IHashService, HashService>();
            services.AddTransient<ISessionService, SessionService>();
            services.AddTransient<IEntityService, EntityService>();

            services.AddCors();
            services.AddControllers();

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var dataSection = Configuration.GetSection("Data");
            var tempSection = Configuration.GetSection("Temp");

            var config = new StartupServiceConfig()
            {
                SecretKey = tempSection.GetValue<string>("JWTSecret"),
                ContentConString = dataSection.GetValue<string>("ContentConnectionString")
            };

            //Assign the email config from the "Email" section (must exist prior, see how we create an object above)
            Configuration.Bind("Email", config.EmailConfig);
            Configuration.Bind("Language", config.LanguageConfig);

            ConfigureBasicServices(services, config);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //Wide open??? Fix this later maybe!!!
            app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}