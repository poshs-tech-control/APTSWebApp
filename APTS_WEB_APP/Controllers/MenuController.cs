using APTS_WEB_APP.DataIntraction;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;

namespace APTS_WEB_APP.Controllers
{
    public class MenuController : Controller
    {
        public ActionResult Menu()
        {
            if (Session["UserCode"] == null)
            {
                return RedirectToAction("Login", "Home");
            }

            ViewBag.MenuAccess = Session["MenuAccess"] as List<DataAccess.MenuAccessItem>
                ?? new List<DataAccess.MenuAccessItem>();

            return View();
        }

        public ActionResult Settings()
        {
            ViewBag.SelectedLanguage = GetSelectedLanguageCode();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Settings(string language)
        {
            string languageCode = NormalizeLanguageCode(language);
            string languageName = GetLanguageName(languageCode);

            Session["Language"] = languageName;
            Session["LanguageCode"] = languageCode;
            SaveLanguageCookie("APTS_LANGUAGE", languageName);
            SaveLanguageCookie("APTS_LANGUAGE_CODE", languageCode);
            SaveGoogleTranslateCookie(languageCode);

            ViewBag.SelectedLanguage = languageCode;
            ViewBag.SettingsMessage = "Language setting saved successfully.";
            return View();
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
