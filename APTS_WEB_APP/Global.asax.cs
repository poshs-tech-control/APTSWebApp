using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            LogUnhandledException(exception);
            var message = exception == null || string.IsNullOrWhiteSpace(exception.Message)
                ? "An unexpected error occurred while processing your request."
                : exception.Message;

            Server.ClearError();
            Response.Clear();
            if (Request != null && Request.Path.IndexOf("/Error", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                Response.Write("An unexpected error occurred while processing your request.");
                return;
            }

            Response.Redirect("~/Error");
        }

        private void LogUnhandledException(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            try
            {
                string logDirectory = Server.MapPath("~/App_Data/Logs");
                WriteExceptionLog(logDirectory, exception);
            }
            catch
            {
                try
                {
                    string fallbackDirectory = Path.Combine(Path.GetTempPath(), "APTS_WEB_APP", "Logs");
                    WriteExceptionLog(fallbackDirectory, exception);
                }
                catch
                {
                    // Avoid throwing from the global error handler.
                }
            }
        }

        private void WriteExceptionLog(string logDirectory, Exception exception)
        {
            Directory.CreateDirectory(logDirectory);

            string logPath = Path.Combine(logDirectory, "UnhandledErrors-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
            var logEntry = new StringBuilder();
            logEntry.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            logEntry.AppendLine("URL: " + (Request == null ? string.Empty : Convert.ToString(Request.Url)));
            logEntry.AppendLine("UserHostAddress: " + (Request == null ? string.Empty : Request.UserHostAddress));
            logEntry.AppendLine("UserAgent: " + (Request == null ? string.Empty : Request.UserAgent));
            logEntry.AppendLine(exception.ToString());
            logEntry.AppendLine(new string('-', 100));

            File.AppendAllText(logPath, logEntry.ToString());
        }
    }
}
