using System.Linq;
using ConsoleApp.Grpc;
using ConsoleApp.Grpc.HealthCheck;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ConsoleApp
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = (diag, httpContext) =>
                {
                    diag.Set("RequestHeaders",
                        httpContext.Request.Headers.ToDictionary(h => h.Key, 
                            h => h.Value.ToString()), true);
                };
            });
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<MyCalculator>();
                endpoints.MapGrpcService<HealthCheckService<MyCalculator>>();

                endpoints.MapGet("/",
                    async context =>
                    {
                        await context.Response.WriteAsync(
                            "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                    });
            });
            // HealthCheck middleware
            app.UseHealthChecks("/hc", new HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
        }
    }
}