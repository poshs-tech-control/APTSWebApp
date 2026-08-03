using System.Web;
using System.Web.Mvc;
using APTS_WEB_APP.Filters;

namespace APTS_WEB_APP
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new SessionSecurityFilterAttribute());
            filters.Add(new ServerInputValidationFilterAttribute());
        }
    }
}

