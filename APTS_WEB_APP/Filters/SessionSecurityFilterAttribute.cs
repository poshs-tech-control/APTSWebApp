using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using APTS_WEB_APP.DataIntraction;

namespace APTS_WEB_APP.Filters
{
    public class SessionSecurityFilterAttribute : ActionFilterAttribute
    {
        private static readonly Dictionary<string, string[]> ControllerAccessAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "MemberMaster", new[] { "Member Master", "MemberMaster", "Member Manufacturing", "MemberManufacturing" } },
            { "InternalTransfer", new[] { "Internal Transfer", "InternalTransfer", "Member Dispatch", "MemberDispatch", "Internal Transfer Challan" } },
            { "MemberReceipt", new[] { "Member Receipt", "MemberReceipt", "Member Receive", "MemberReceived" } },
            { "PalletOrder", new[] { "Pallet Order", "PalletOrder" } },
            { "PalletAssemble", new[] { "Pallet Assemble", "PalletAssemble", "Pallet Assembly", "PalletAssembly" } },
            { "PalletDisassembleClient", new[] { "Pallet Diss Client", "PalletDissClient", "Pallet Disassemble Client", "PalletDisassembleClient", "Pallet Dismantle Client", "PalletDismantleClient", "Pallet Dismentle Client", "PalletDismentleClient" } },
            { "PalletPreDispatch", new[] { "Pre Dispatch Details", "PreDispatchDetails", "Pallet Pre Dispatch", "PalletPreDispatch" } },
            { "PalletDispatch", new[] { "Pallet Dispatch", "PalletDispatch" } },
            { "PalletDisassembleCustomer", new[] { "Pallet Diss Customer", "PalletDissCustomer", "Pallet Disassemble Customer", "PalletDisassembleCustomer", "Pallet Dismantle Customer", "PalletDismantleCustomer", "Pallet Dismentle Customer", "PalletDismentleCustomer" } },
            { "MemberCollection", new[] { "Member Collection", "MemberCollection" } },
            { "CollectionInternalTransfer", new[] { "Coll Int Transfer", "CollIntTransfer", "Collection Internal Transfer", "CollectionInternalTransfer", "Collection Int Transfer", "CollectionIntTransfer" } },
            { "Report", new[] { "Reports", "Report" } },
            { "MemberTagging", new[] { "Member Tagging", "MemberTagging", "Member Master", "MemberMaster", "Member Manufacturing", "MemberManufacturing" } }
        };

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ApplyNoCache(filterContext.HttpContext.Response);

            if (AllowsAnonymousAccess(filterContext))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            object userCode = filterContext.HttpContext.Session == null
                ? null
                : filterContext.HttpContext.Session["UserCode"];

            if (userCode == null || string.IsNullOrWhiteSpace(Convert.ToString(userCode)))
            {
                SetLoginRequiredResult(filterContext, "Session expired. Please login again.");
                return;
            }

            if (!HasModuleAccess(filterContext))
            {
                SetLoginRequiredResult(filterContext, "Access denied for this module. Please login with authorized user.");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        private static bool AllowsAnonymousAccess(ActionExecutingContext filterContext)
        {
            string controller = Convert.ToString(filterContext.RouteData.Values["controller"]);
            string action = Convert.ToString(filterContext.RouteData.Values["action"]);

            if (string.Equals(controller, "Error", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(controller, "Home", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(action, "Login", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(action, "SetLanguage", StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasModuleAccess(ActionExecutingContext filterContext)
        {
            string controller = Convert.ToString(filterContext.RouteData.Values["controller"]);
            if (string.IsNullOrWhiteSpace(controller)
                || string.Equals(controller, "Home", StringComparison.OrdinalIgnoreCase)
                || string.Equals(controller, "Error", StringComparison.OrdinalIgnoreCase)
                || string.Equals(controller, "Menu", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] aliases;
            if (!ControllerAccessAliases.TryGetValue(controller, out aliases))
            {
                return true;
            }

            var menuAccess = filterContext.HttpContext.Session["MenuAccess"] as IEnumerable<DataAccess.MenuAccessItem>;
            HashSet<string> allowedKeys = BuildAllowedKeys(menuAccess);
            return aliases.Any(alias => allowedKeys.Contains(NormalizeAccessKey(alias)));
        }

        private static HashSet<string> BuildAllowedKeys(IEnumerable<DataAccess.MenuAccessItem> menuAccess)
        {
            var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (menuAccess == null)
            {
                return allowedKeys;
            }

            foreach (DataAccess.MenuAccessItem item in menuAccess)
            {
                if (item == null)
                {
                    continue;
                }

                AddAllowedKey(allowedKeys, item.MenuDTCode);
                AddAllowedKey(allowedKeys, item.MenuDTName);
                AddAllowedKey(allowedKeys, item.TransactionID);
            }

            return allowedKeys;
        }

        private static void AddAllowedKey(HashSet<string> allowedKeys, string value)
        {
            string key = NormalizeAccessKey(value);
            if (!string.IsNullOrWhiteSpace(key))
            {
                allowedKeys.Add(key);
            }
        }

        private static string NormalizeAccessKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = new List<char>();
            foreach (char ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    chars.Add(char.ToUpperInvariant(ch));
                }
            }

            return new string(chars.ToArray());
        }

        private static void SetLoginRequiredResult(ActionExecutingContext filterContext, string message)
        {
            if (IsAjaxRequest(filterContext.HttpContext.Request))
            {
                filterContext.Result = new JsonResult
                {
                    Data = new { success = false, isValid = false, message = message },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                return;
            }

            filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary
            {
                { "controller", "Home" },
                { "action", "Login" }
            });
        }

        private static bool IsAjaxRequest(HttpRequestBase request)
        {
            if (request == null)
            {
                return false;
            }

            return string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                || (request.AcceptTypes != null && Array.Exists(request.AcceptTypes, value => value != null && value.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static void ApplyNoCache(HttpResponseBase response)
        {
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Cache.SetNoStore();
            response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
            response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            response.Cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
            response.Headers["Pragma"] = "no-cache";
            response.Headers["Expires"] = "-1";
        }
    }
}