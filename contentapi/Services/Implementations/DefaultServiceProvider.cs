using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.ViewConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace contentapi.Services.Implementations
{
    public class DefaultServiceProvider
    {
        /// <summary>
        /// A class specifically to allow an essentially generic function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class HackOptions<T> where T : class
        {
            public HackOptions(IServiceCollection services, IConfiguration config)
            {
                var section = config.GetSection(typeof(T).Name);

                services.Configure<T>(section);
                services.AddTransient<T>(p => 
                    p.GetService<IOptionsMonitor<T>>().CurrentValue);
            }
        };

        public void AddDefaultServices(IServiceCollection services)
        {
            services.AddTransient<IEmailService, EmailService>();
            services.AddTransient<ILanguageService, LanguageService>();
            services.AddTransient<ITokenService, TokenService>();
            services.AddTransient<IHashService, HashService>();
            services.AddTransient<IPermissionService, PermissionService>();
            services.AddTransient<IActivityService, ActivityService>();
            services.AddTransient<IHistoryService, HistoryService>();
            services.AddTransient(typeof(IDecayer<>), typeof(Decayer<>));
            services.AddTransient(typeof(ITempTokenService<>), typeof(TempTokenService<>));

            services.AddTransient<ActivityViewService>();
            services.AddTransient<CategoryViewService>();
            services.AddTransient<CommentViewService>();
            services.AddTransient<ContentViewService>();
            services.AddTransient<FileViewService>();
            services.AddTransient<UserViewService>();

            services.AddTransient<UserViewConverter>();
            services.AddTransient<ContentViewConvter>();
            services.AddTransient<CategoryViewConverter>();
            services.AddTransient<FileViewConverter>();

            //We need automapper for our view services
            services.AddAutoMapper(GetType());

            //And now, the service config that goes into EVERY controller.
            services.AddTransient<ViewServicePack>();

            //Just always good to be safe!
            Keys.EnsureAllUnique();
        }

        public void AddConfiguration<T>(IServiceCollection services, IConfiguration config) where T : class
        {
            new HackOptions<T>(services, config);
        }

        public void AddServiceConfigurations(IServiceCollection services, IConfiguration config)
        {
            AddConfiguration<EmailConfig>(services, config);
            AddConfiguration<LanguageConfig>(services, config);
            AddConfiguration<SystemConfig>(services, config);
            AddConfiguration<TempTokenServiceConfig>(services, config);
            AddConfiguration<TokenServiceConfig>(services, config);
            services.AddSingleton<HashConfig>();
        }
    }
}