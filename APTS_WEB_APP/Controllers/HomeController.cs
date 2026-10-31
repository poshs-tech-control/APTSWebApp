using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Caching;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APTS_WEB_APP.Controllers
{
    public class HomeController : Controller
    {
        private const int MaxFailedLoginAttempts = 5;
        private static readonly TimeSpan LoginLockoutDuration = TimeSpan.FromMinutes(15);
        private readonly DataAccess dataAccess = new DataAccess();
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Login()
        {
            EndCurrentSession();
            ApplyNoCacheHeaders();

            ViewBag.SelectedLanguage = GetSelectedLanguageCode();
            LoadLoginLocations();
            return View();
        }

        public ActionResult Error()
        {
            return View();
        }

        public ActionResult Logout()
        {
            long loginHistoryId = GetSessionLong("UserLoginHistoryID");
            string userCode = Convert.ToString(Session["UserCode"]);
            dataAccess.UpdateUserLoginHistoryLogout(loginHistoryId, userCode);

            EndCurrentSession();
            ApplyNoCacheHeaders();

            return RedirectToAction("Login", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginInputModel request)
        {
            ClearCurrentSessionValues();
            ApplyNoCacheHeaders();

            request = request ?? new LoginInputModel();
            string loginId = (request.LoginId ?? string.Empty).Trim();
            string password = request.Password ?? string.Empty;
            string locations = (request.Locations ?? string.Empty).Trim();

            List<LoginLocationItem> loginLocations = LoadLoginLocations();
            ViewBag.LoginId = loginId;
            ViewBag.SelectedLocationCode = locations;

            if (!ModelState.IsValid)
            {
                ViewBag.LoginError = GetFirstModelError();
                ViewBag.LoginPopupMessage = ViewBag.LoginError;
                return View();
            }

            LoginLocationItem selectedLocation = loginLocations.FirstOrDefault(location =>
                string.Equals(location.LocationCode, locations, StringComparison.OrdinalIgnoreCase));

            if (selectedLocation == null || string.IsNullOrWhiteSpace(selectedLocation.LocationCode))
            {
                ViewBag.LoginError = "Please select valid Location.";
                ViewBag.LoginPopupMessage = ViewBag.LoginError;
                return View();
            }

            string lockoutMessage;
            if (IsLoginLocked(loginId, out lockoutMessage))
            {
                ViewBag.LoginError = lockoutMessage;
                ViewBag.LoginPopupMessage = lockoutMessage;
                return View();
            }

            EncryptDecrypt encrypt = new EncryptDecrypt();
            string encryptedPassword = encrypt.Encrypt(password);
            password = string.Empty;
            DataAccess.LoginUser loginUser = dataAccess.ValidateLogin(loginId, encryptedPassword);

            if (loginUser == null || string.IsNullOrWhiteSpace(loginUser.UserCode))
            {
                RegisterFailedLoginAttempt(loginId);
                ViewBag.LoginError = "Invalid Login Id or Password.";
                return View();
            }

            ResetFailedLoginAttempts(loginId);

            if (!dataAccess.ValidateLocationAccess(loginUser.UserCode, selectedLocation.LocationCode))
            {
                ViewBag.LoginError = "Access not granted for selected location.";
                ViewBag.LoginPopupMessage = "Access not granted for selected location.";
                return View();
            }

            Session["UserCode"] = loginUser.UserCode;
            Session["USERCODE"] = loginUser.UserCode;
            Session["UserFullName"] = loginUser.UserFullName;
            Session["USERFULLNAME"] = loginUser.UserFullName;
            Session["UserType"] = loginUser.UserType;
            Session["USERTYPE"] = loginUser.UserType;
            Session["UserName"] = loginUser.UserName;
            Session["USERNAME"] = loginUser.UserName;
            Session["UserRole"] = loginUser.UserRole;
            Session["USERROLE"] = loginUser.UserRole;
            Session["MenuAccess"] = loginUser.MenuAccess ?? new List<DataAccess.MenuAccessItem>();
            Session["MENUACCESS"] = Session["MenuAccess"];
            Session["CustomerCode"] = selectedLocation.CustomerCode;
            Session["CUSTOMERCODE"] = selectedLocation.CustomerCode;
            Session["LocationCode"] = selectedLocation.LocationCode;
            Session["LOCATIONCODE"] = selectedLocation.LocationCode;
            Session["LocationTitle"] = selectedLocation.LocationTitle;
            Session["LOCATIONTITLE"] = selectedLocation.LocationTitle;
            Session["FromLocationTitle"] = selectedLocation.LocationTitle;
            Session["LocationType"] = selectedLocation.LocationType;
            Session["LOCATIONTYPE"] = selectedLocation.LocationType;

            long loginHistoryId = dataAccess.InsertUserLoginHistory(
                loginUser,
                selectedLocation,
                dataAccess.GetIpAddress(this),
                BuildLoginAuditTrail(loginId, loginUser, selectedLocation));
            Session["UserLoginHistoryID"] = loginHistoryId;
            Session["USERLOGINHISTORYID"] = loginHistoryId;

            return RedirectToAction("Menu", "Menu");
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetLanguage(string language)
        {
            string languageCode = NormalizeLanguageCode(language);
            string languageName = GetLanguageName(languageCode);

            Session["Language"] = languageName;
            Session["LanguageCode"] = languageCode;
            SaveLanguageCookie("APTS_LANGUAGE", languageName);
            SaveLanguageCookie("APTS_LANGUAGE_CODE", languageCode);
            SaveGoogleTranslateCookie(languageCode);

            return RedirectToAction("Login");
        }



        private string GetFirstModelError()
        {
            foreach (ModelState modelState in ModelState.Values)
            {
                foreach (ModelError error in modelState.Errors)
                {
                    if (!string.IsNullOrWhiteSpace(error.ErrorMessage))
                    {
                        return error.ErrorMessage;
                    }
                }
            }

            return "Please enter valid input.";
        }
        private static bool IsLoginLocked(string loginId, out string message)
        {
            message = string.Empty;
            LoginAttemptState state = GetLoginAttemptState(loginId);
            if (state == null || state.LockedUntil <= DateTime.UtcNow)
            {
                if (state != null && state.LockedUntil != DateTime.MinValue)
                {
                    ResetFailedLoginAttempts(loginId);
                }

                return false;
            }

            int remainingMinutes = Math.Max(1, (int)Math.Ceiling((state.LockedUntil - DateTime.UtcNow).TotalMinutes));
            message = "Account locked due to multiple failed login attempts. Please try again after " + remainingMinutes + " minute(s).";
            return true;
        }

        private static void RegisterFailedLoginAttempt(string loginId)
        {
            string key = BuildLoginAttemptCacheKey(loginId);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            LoginAttemptState state = HttpRuntime.Cache[key] as LoginAttemptState ?? new LoginAttemptState();
            state.FailedAttempts++;
            if (state.FailedAttempts >= MaxFailedLoginAttempts)
            {
                state.LockedUntil = DateTime.UtcNow.Add(LoginLockoutDuration);
            }

            HttpRuntime.Cache.Insert(key, state, null, DateTime.UtcNow.Add(LoginLockoutDuration), Cache.NoSlidingExpiration);
        }

        private static void ResetFailedLoginAttempts(string loginId)
        {
            string key = BuildLoginAttemptCacheKey(loginId);
            if (!string.IsNullOrWhiteSpace(key))
            {
                HttpRuntime.Cache.Remove(key);
            }
        }

        private static LoginAttemptState GetLoginAttemptState(string loginId)
        {
            string key = BuildLoginAttemptCacheKey(loginId);
            return string.IsNullOrWhiteSpace(key) ? null : HttpRuntime.Cache[key] as LoginAttemptState;
        }

        private static string BuildLoginAttemptCacheKey(string loginId)
        {
            return string.IsNullOrWhiteSpace(loginId) ? string.Empty : "LoginLockout:" + loginId.Trim().ToUpperInvariant();
        }

        private class LoginAttemptState
        {
            public int FailedAttempts { get; set; }

            public DateTime LockedUntil { get; set; }
        }

        private void EndCurrentSession()
        {
            ClearCurrentSessionValues();
            Session.Abandon();

            if (Request.Cookies["ASP.NET_SessionId"] != null)
            {
                var sessionCookie = new HttpCookie("ASP.NET_SessionId", string.Empty)
                {
                    Expires = DateTime.UtcNow.AddDays(-1),
                    HttpOnly = true,
                    Path = "/"
                };
                Response.Cookies.Add(sessionCookie);
            }
        }


        private void ClearCurrentSessionValues()
        {
            Session.Clear();
            Session.RemoveAll();
        }


        private void ApplyNoCacheHeaders()
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
            Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            Response.Cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "-1";
        }

        private List<LoginLocationItem> LoadLoginLocations()
        {
            List<LoginLocationItem> locations = dataAccess.LoadLoginLocationList();
            ViewBag.Locations = locations;
            return locations;
        }

        private long GetSessionLong(string key)
        {
            long value;
            return long.TryParse(Convert.ToString(Session[key]), out value) ? value : 0;
        }

        private string BuildLoginAuditTrail(string loginId, DataAccess.LoginUser loginUser, LoginLocationItem selectedLocation)
        {
            var auditTrail = new JObject
            {
                ["LoginId"] = loginId ?? string.Empty,
                ["UserCode"] = loginUser == null ? string.Empty : loginUser.UserCode,
                ["UserName"] = loginUser == null ? string.Empty : loginUser.UserName,
                ["UserFullName"] = loginUser == null ? string.Empty : loginUser.UserFullName,
                ["UserRole"] = loginUser == null ? string.Empty : loginUser.UserRole,
                ["LocationCode"] = selectedLocation == null ? string.Empty : selectedLocation.LocationCode,
                ["LocationTitle"] = selectedLocation == null ? string.Empty : selectedLocation.LocationTitle,
                ["LocationType"] = selectedLocation == null ? string.Empty : selectedLocation.LocationType,
                ["CustomerCode"] = selectedLocation == null ? string.Empty : selectedLocation.CustomerCode,
                ["UserAgent"] = Request == null ? string.Empty : (Request.UserAgent ?? string.Empty),
                ["AuthenticatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            return auditTrail.ToString(Formatting.None);
        }

        private string GetSelectedLanguageCode()
        {
            string sessionLanguageCode = Convert.ToString(Session["LanguageCode"]);
            if (!string.IsNullOrWhiteSpace(sessionLanguageCode))
            {
                return NormalizeLanguageCode(sessionLanguageCode);
            }

            HttpCookie languageCookie = Request.Cookies["APTS_LANGUAGE_CODE"];
            return NormalizeLanguageCode(languageCookie == null ? null : languageCookie.Value);
        }

        private string NormalizeLanguageCode(string languageCode)
        {
            switch ((languageCode ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "hi":
                case "mr":
                case "gu":
                case "ta":
                case "te":
                case "kn":
                case "ml":
                    return languageCode.Trim().ToLowerInvariant();
                default:
                    return "en";
            }
        }

        private string GetLanguageName(string languageCode)
        {
            switch (NormalizeLanguageCode(languageCode))
            {
                case "hi":
                    return "Hindi";
                case "mr":
                    return "Marathi";
                case "gu":
                    return "Gujarati";
                case "ta":
                    return "Tamil";
                case "te":
                    return "Telugu";
                case "kn":
                    return "Kannada";
                case "ml":
                    return "Malayalam";
                default:
                    return "English";
            }
        }

        private void SaveLanguageCookie(string name, string value)
        {
            HttpCookie cookie = new HttpCookie(name, value)
            {
                Expires = DateTime.Now.AddYears(1),
                HttpOnly = false,
                Path = "/"
            };

            Response.Cookies.Add(cookie);
        }

        private void SaveGoogleTranslateCookie(string languageCode)
        {
            languageCode = NormalizeLanguageCode(languageCode);

            if (languageCode == "en")
            {
                ExpireLanguageCookie("googtrans");
                return;
            }

            SaveLanguageCookie("googtrans", "/en/" + languageCode);
        }

        private void ExpireLanguageCookie(string name)
        {
            HttpCookie cookie = new HttpCookie(name, string.Empty)
            {
                Expires = DateTime.Now.AddDays(-1),
                HttpOnly = false,
                Path = "/"
            };

            Response.Cookies.Add(cookie);
        }
    }
}


