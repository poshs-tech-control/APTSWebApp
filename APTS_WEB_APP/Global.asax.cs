using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace APTS_WEB_APP
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }


        protected void Application_EndRequest()
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
            Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            Response.Cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
            Response.Headers.Set("Pragma", "no-cache");
            Response.Headers.Set("Expires", "-1");
            Response.Headers.Set("X-Frame-Options", "SAMEORIGIN");
            Response.Headers.Set("Content-Security-Policy", "frame-ancestors 'self';");
        }
        protected void Application_Error()
        {
            var exception = Server.GetLastError();
            var message = exception == null || string.IsNullOrWhiteSpace(exception.Message)
                ? "An unexpected error occurred while processing your request."
                : exception.Message;


            Server.ClearError();
            Response.Clear();
            Response.Redirect("~/Home/Error");
        }
    }
}
