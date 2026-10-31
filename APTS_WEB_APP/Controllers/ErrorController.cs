using System.Net;
using System.Web.Mvc;

namespace APTS_WEB_APP.Controllers
{
    public class ErrorController : Controller
    {
        public ActionResult Index()
        {
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/Error.cshtml");
        }

        public ActionResult NotFound()
        {
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/Error.cshtml");
        }
    }
}
