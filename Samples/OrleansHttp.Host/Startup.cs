using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Orleans.Http;
using OrleansHttp.Grains;

namespace OrleansHttp.Host
{
    public class Startup
    {
        public const string SECRET = "THIS IS OUR AWESOME SUPER SECRET!!!";
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddAuthentication(opt =>
            {
                opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(opt =>
            {
                opt.RequireHttpsMetadata = false;
                opt.SaveToken = true;
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SECRET)),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });
            services.AddAuthorization();

            services
                .AddGrainRouter()
                .AddJsonMediaType()
                .AddProtobufMediaType();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrains("grains");
            });
            app.UseRouteGrainProviders(rgppb =>
            {
                rgppb.RegisterRouteGrainProvider<RandomGuidRouteGrainProvider>(nameof(RandomGuidRouteGrainProvider));
            });
        }
    }
}