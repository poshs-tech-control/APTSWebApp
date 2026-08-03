using System.Net;
using System.Web.Mvc;

namespace APTS_WEB_APP.Controllers
{
    public class ErrorController : Controller
    {
        public ActionResult Index()
        {
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/Error.cshtml");
        }

        public ActionResult NotFound()
        {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/Error.cshtml");
        }
    }
}
