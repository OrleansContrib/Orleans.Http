using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Orleans.Http.Host
{
    public class WebHostStartup
    {
        public void Configure(IApplicationBuilder app)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.Map(PathString.FromUriComponent("/api"), builder =>
            {
                builder.UseMvc();
                builder.Map(
                    PathString.FromUriComponent("/invoke"),
                    subBuilder => subBuilder.UseMiddleware<GrainCallInvokerMiddleware>());
            });

            app.UseHealthChecks("/health");
        }
    }
}