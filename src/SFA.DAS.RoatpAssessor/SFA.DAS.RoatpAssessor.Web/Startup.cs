﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.WsFederation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using SFA.DAS.AdminService.Common;
using SFA.DAS.AdminService.Common.Extensions;
using SFA.DAS.RoatpAssessor.Web.Domain;
using SFA.DAS.RoatpAssessor.Web.Infrastructure.ApiClients;
using SFA.DAS.RoatpAssessor.Web.Infrastructure.ApiClients.TokenService;
using SFA.DAS.RoatpAssessor.Web.Services;
using SFA.DAS.RoatpAssessor.Web.Settings;
using SFA.DAS.RoatpAssessor.Web.Validators;

namespace SFA.DAS.RoatpAssessor.Web
{
    [ExcludeFromCodeCoverage]
    public class Startup
    {
        private const string ServiceName = "SFA.DAS.RoatpAssessor";
        private const string Version = "1.0";
        private const string Culture = "en-GB";

        private readonly IConfiguration _configuration;
        private readonly IHostingEnvironment _env;
        private readonly ILogger<Startup> _logger;

        public IWebConfiguration ApplicationConfiguration { get; set; }

        public Startup(IConfiguration configuration, IHostingEnvironment env, ILogger<Startup> logger)
        {
            _env = env;
            _logger = logger;
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            ConfigureApplicationConfiguration();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => false; // Default is true, make it false
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            AddAuthentication(services);

            services.Configure<RequestLocalizationOptions>(options =>
            {
                options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(Culture);
                options.SupportedCultures = new List<CultureInfo> { new CultureInfo(Culture) };
                options.RequestCultureProviders.Clear();
            });

            services.AddMvc(options =>
                {
                    //options.Filters.Add<CheckSessionFilter>();
                    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
                })
                // NOTE: Can we move this to 2.2 to match the version of .NET Core we're coding against?
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1).AddJsonOptions(options =>
                {
                    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                });

            services.AddSession(opt => { opt.IdleTimeout = TimeSpan.FromHours(1); });

            if (!_env.IsDevelopment())
            {
                services.AddDistributedRedisCache(options =>
                {
                    options.Configuration = ApplicationConfiguration.SessionRedisConnectionString;
                });
            }

            AddAntiforgery(services);

            services.AddHealthChecks();

            services.AddApplicationInsightsTelemetry();

            ConfigureHttpClients(services);
            ConfigureDependencyInjection(services);
        }

        private void ConfigureApplicationConfiguration()
        {
            try
            {
                ApplicationConfiguration = ConfigurationService.GetConfig(_configuration["EnvironmentName"], _configuration["ConfigurationStorageConnectionString"], Version, ServiceName).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to retrieve Application Configuration", ex);
                throw;
            }
        }

        private void AddAuthentication(IServiceCollection services)
        {
            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = WsFederationDefaults.AuthenticationScheme;
                sharedOptions.DefaultSignOutScheme = WsFederationDefaults.AuthenticationScheme;
            }).AddWsFederation(options =>
            {
                options.Wtrealm = ApplicationConfiguration.StaffAuthentication.WtRealm;
                options.MetadataAddress = ApplicationConfiguration.StaffAuthentication.MetadataAddress;
                options.TokenValidationParameters.RoleClaimType = Roles.RoleClaimType;
            }).AddCookie();
        }

        private void AddAntiforgery(IServiceCollection services)
        {
            services.AddAntiforgery(options => options.Cookie = new CookieBuilder() { Name = ".RoatpAssessor.Staff.AntiForgery", HttpOnly = false });
        }

        private void ConfigureHttpClients(IServiceCollection services)
        {
            var acceptHeaderName = "Accept";
            var acceptHeaderValue = "application/json";
            var handlerLifeTime = TimeSpan.FromMinutes(5);

            services.AddHttpClient<IRoatpApplicationApiClient, RoatpApplicationApiClient>(config =>
            {
                config.BaseAddress = new Uri(ApplicationConfiguration.RoatpApplicationApiAuthentication.ApiBaseAddress);
                config.DefaultRequestHeaders.Add(acceptHeaderName, acceptHeaderValue);
            })
            .SetHandlerLifetime(handlerLifeTime)
            .AddPolicyHandler(GetRetryPolicy());
        }

        private void ConfigureDependencyInjection(IServiceCollection services)
        {
            services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();

            services.AddTransient(x => ApplicationConfiguration);

            services.AddTransient<IAssessorDashboardOrchestrator, AssessorDashboardOrchestrator>();
            services.AddTransient<IModeratorDashboardOrchestrator, ModeratorDashboardOrchestrator>();
            services.AddTransient<IClarificationDashboardOrchestrator, ClarificationDashboardOrchestrator>();
            services.AddTransient<IOutcomeDashboardOrchestrator, OutcomeDashboardOrchestrator>();
            services.AddTransient<IRoatpApplicationTokenService, RoatpApplicationTokenService>();
            services.AddTransient<IClarificationOutcomeOrchestrator, ClarificationOutcomeOrchestrator>();
            services.AddTransient<IRoatpAssessorApiClient>(x => new RoatpAssessorApiClient(
                ApplicationConfiguration.RoatpApplicationApiAuthentication.ApiBaseAddress,
                x.GetService<ILogger<RoatpAssessorApiClient>>(),
                x.GetService<IRoatpApplicationTokenService>()));

            services.AddTransient<IRoatpModerationApiClient>(x => new RoatpModerationApiClient(
                ApplicationConfiguration.RoatpApplicationApiAuthentication.ApiBaseAddress,
                x.GetService<ILogger<RoatpModerationApiClient>>(),
                x.GetService<IRoatpApplicationTokenService>()));

            services.AddTransient<IRoatpClarificationApiClient>(x => new RoatpClarificationApiClient(
                ApplicationConfiguration.RoatpApplicationApiAuthentication.ApiBaseAddress,
                x.GetService<ILogger<RoatpClarificationApiClient>>(),
                x.GetService<IRoatpApplicationTokenService>()));

            services.AddTransient<IAssessorOverviewOrchestrator, AssessorOverviewOrchestrator>();
            services.AddTransient<IModeratorOverviewOrchestrator, ModeratorOverviewOrchestrator>();
            services.AddTransient<IClarificationOverviewOrchestrator, ClarificationOverviewOrchestrator>();            
            services.AddTransient<IOutcomeOverviewOrchestrator, OutcomeOverviewOrchestrator>();

            services.AddTransient<ISupplementaryInformationService, SupplementaryInformationService>();

            services.AddTransient<IAssessorSectionReviewOrchestrator, AssessorSectionReviewOrchestrator>();
            services.AddTransient<IAssessorPageValidator, AssessorPageValidator>();
            services.AddTransient<IAssessorOutcomeValidator, AssessorOutcomeValidator>();

            services.AddTransient<IModeratorSectionReviewOrchestrator, ModeratorSectionReviewOrchestrator>();
            services.AddTransient<IModeratorPageValidator, ModeratorPageValidator>();
            services.AddTransient<IModeratorOutcomeValidator, ModeratorOutcomeValidator>();
            services.AddTransient<IModeratorOutcomeOrchestrator, ModeratorOutcomeOrchestrator>();

            services.AddTransient<IClarificationSectionReviewOrchestrator, ClarificationSectionReviewOrchestrator>();
            services.AddTransient<IClarificationPageValidator, ClarificationPageValidator>();
            services.AddTransient<IClarificationOutcomeValidator, ClarificationOutcomeValidator>();

            services.AddTransient<IOutcomeSectionReviewOrchestrator, OutcomeSectionReviewOrchestrator>();

            DependencyInjection.ConfigureDependencyInjection(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseSession();
            app.UseRequestLocalization();
            app.UseStatusCodePagesWithReExecute("/ErrorPage/{0}");
            app.UseSecurityHeaders();
            app.UseHealthChecks("/health");
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.NotFound)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                    retryAttempt)));
        }
    }
}
