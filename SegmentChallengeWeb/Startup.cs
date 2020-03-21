using System;
using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using SegmentChallengeWeb.Configuration;

namespace SegmentChallengeWeb {
    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            var siteConfigSection = Configuration.GetSection("SegmentChallenge");
            services.Configure<SegmentChallengeConfiguration>(siteConfigSection);
            services.Configure<StravaConfiguration>(Configuration.GetSection("Strava"));
            services.Configure<MySqlConfiguration>(Configuration.GetSection("MySql"));

            services.AddLogging();
            services.AddMvc();

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration => {
                configuration.RootPath = "ClientApp/build";
            });

            services.AddHsts(options => { options.MaxAge = TimeSpan.FromHours(1); });

            var siteConfiguration = new SegmentChallengeConfiguration();
            siteConfigSection.Bind(siteConfiguration);

            services.AddAuthentication("JwtCookie")
                .AddScheme<JwtCookieOptions, JwtCookieHandler>(
                    "JwtCookie",
                    options => {
                        options.SecretKey = siteConfiguration.SecretKey;
                        options.ClaimsIssuer = siteConfiguration.BaseUrl;
                    });

            services.AddScoped<Func<DbConnection>>(provider => {
                var configuration =
                    provider.GetRequiredService<IOptions<MySqlConfiguration>>().Value;

                return () => {
                    var builder = new MySqlConnectionStringBuilder {
                        Port = configuration.Port,
                        Server = configuration.Host,
                        Database = configuration.Database,
                        UserID = configuration.User,
                        Password = configuration.Password,
                        CharacterSet = "utf8mb4",
                        SslMode = MySqlSslMode.None,
                        IgnoreCommandTransaction = true
                    };

                    return new MySqlConnection(builder.ToString());
                };
            });

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();

            services.AddSingleton<BackgroundTaskService>();
            services.AddHostedService<BackgroundTaskService>();

            services.AddSingleton<StravaApiHelper>();

            services.AddScoped<EffortRefreshService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            var developmentMode =
                String.Equals(
                    "true",
                    this.Configuration["DevelopmentMode"],
                    StringComparison.OrdinalIgnoreCase
                );

            if (developmentMode) {
                app.UseDeveloperExceptionPage();
            } else {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseAuthentication();

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "api/{controller}/{action=Index}/{id?}");
            });

            app.UseSpa(spa => {
                spa.Options.SourcePath = "ClientApp";

                if (developmentMode) {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });
        }
    }

    public class JwtCookieOptions : AuthenticationSchemeOptions {
        public String SecretKey { get; set; }
    }

    public class JwtCookieHandler : AuthenticationHandler<JwtCookieOptions> {
        private readonly JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

        public JwtCookieHandler(
            IOptionsMonitor<JwtCookieOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock) : base(options, logger, encoder, clock) {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
            var signingKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.Options.SecretKey));

            var idToken = this.Request.Cookies["id_token"];

            if (String.IsNullOrWhiteSpace(idToken)) {
                return Task.FromResult(AuthenticateResult.NoResult());
            } else {
                try {
                    var claims = this.tokenHandler.ValidateToken(
                        idToken,
                        new TokenValidationParameters {
                            RequireSignedTokens = true,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = signingKey,
                            ValidateAudience = true,
                            ValidateIssuer = true,
                            ValidIssuer = this.Options.ClaimsIssuer,
                            ValidAudience = this.Options.ClaimsIssuer
                        },
                        out var token
                    );

                    if (!(token is JwtSecurityToken jwtToken)) {
                        throw new InvalidOperationException();
                    } else if (jwtToken.SignatureAlgorithm != "HS256") {
                        return Task.FromResult(AuthenticateResult.Fail("Invalid algorithm."));
                    } else if (token.ValidTo < DateTime.UtcNow) {
                        return Task.FromResult(AuthenticateResult.Fail("Token expired."));
                    } else if (token.ValidFrom > DateTime.UtcNow) {
                        return Task.FromResult(AuthenticateResult.Fail("Token not yet valid."));
                    } else {
                        return Task.FromResult(AuthenticateResult.Success(new JwtCookieAuthenticationTicket(
                            claims
                        )));
                    }
                } catch (SecurityTokenValidationException ex) {
                    return Task.FromResult(AuthenticateResult.Fail(ex));
                }
            }
        }
    }

    public class JwtCookieAuthenticationTicket : AuthenticationTicket {
        public JwtCookieAuthenticationTicket(
            ClaimsPrincipal principal,
            AuthenticationProperties properties) :
            base(new JwtCookiePrincipal(principal), properties, "JwtCookie") {

        }

        public JwtCookieAuthenticationTicket(ClaimsPrincipal principal) :
            base(new JwtCookiePrincipal(principal), "JwtCookie") {
        }
    }

    public class JwtCookiePrincipal : ClaimsPrincipal {
        public Int64 UserId { get; }

        public JwtCookiePrincipal(ClaimsPrincipal principal) : base(principal) {
            this.UserId = Int64.Parse(
                principal.FindFirstValue("sub") ??
                // Because Microsoft is to fucking good for the god damn "sub" principal
                principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
            );
        }
    }
}
