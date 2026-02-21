using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.Helpers;
namespace ResumableFunctions.MvcUi
{
    public static class Extensions
    {
        public static IMvcBuilder AddResumableFunctionsMvcUi(this IMvcBuilder mvcBuilder)
        {
            mvcBuilder
                .AddApplicationPart(typeof(ResumableFunctionsController).Assembly)
                .AddControllersAsServices();
            mvcBuilder.Services.AddRazorPages();
            return mvcBuilder;
        }


        public static void UseResumableFunctionsUi(this WebApplication app)
        {
            app.UseHangfireDashboard();
            app.MapRazorPages();
            app.UseStaticFiles();


            app.UseRouting();

            app.MapControllerRoute(
                name: "MyArea",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

        }
    }
}