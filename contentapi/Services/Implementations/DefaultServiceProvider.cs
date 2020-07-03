using AutoMapper;
using contentapi.Configs;
using contentapi.Services.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Randomous.EntitySystem;

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
            services.AddTransient<IHistoryService, HistoryService>();
            services.AddSingleton(typeof(IDecayer<>), typeof(Decayer<>)); //Is it safe to make ALL decayers singletons? I don't know... I suppose that's what it's for?
            services.AddTransient(typeof(ITempTokenService<>), typeof(TempTokenService<>));

            services.AddTransient<ActivityViewService>();
            services.AddTransient<CategoryViewService>();
            services.AddTransient<CommentViewService>();
            services.AddTransient<ContentViewService>();
            services.AddTransient<FileViewService>();
            services.AddTransient<UserViewService>();
            services.AddTransient<WatchViewService>();
            services.AddTransient<VoteViewService>();
            services.AddTransient<ModuleViewService>();

            //Special services
            services.AddTransient<RelationListenerService>();
            services.AddTransient<ChainService>();
            services.AddSingleton<ModuleService>(); //Because of the special nature of modules, everyone should use the same one.

            services.AddTransient<ActivityViewSource>();
            services.AddTransient<CategoryViewSource>();
            services.AddTransient<CommentViewSource>();
            services.AddTransient<ContentViewSource>();
            services.AddTransient<FileViewSource>();
            services.AddTransient<UserViewSource>();
            services.AddTransient<WatchViewSource>();
            services.AddTransient<VoteViewSource>();
            services.AddTransient<ModuleViewSource>();

            services.AddTransient((p) => new ChainServices()
            {
                file = p.GetService<FileViewService>(),
                user = p.GetService<UserViewService>(),
                content = p.GetService<ContentViewService>(),
                category = p.GetService<CategoryViewService>(),
                comment = p.GetService<CommentViewService>(),
                activity = p.GetService<ActivityViewService>(),
                watch = p.GetService<WatchViewService>(),
                vote = p.GetService<VoteViewService>(),
                provider = p.GetService<IEntityProvider>(),
                module = p.GetService<ModuleViewService>()
            });

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
            AddConfiguration<ChainServiceConfig>(services, config);
            AddConfiguration<ModuleServiceConfig>(services, config);
            AddConfiguration<RelationListenerServiceConfig>(services, config);
            //AddConfiguration<DocumentationConfig>(services, config);
            services.AddSingleton<HashConfig>();
        }
    }
}