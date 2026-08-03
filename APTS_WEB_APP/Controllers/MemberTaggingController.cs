using System;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class MemberTaggingController : Controller
    {
        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult MemberTagging()
        {
            return View();
        }

        [HttpGet]
        public JsonResult ShowMember(string memberPlate)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(memberPlate);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan or enter Member Plate." }, JsonRequestBehavior.AllowGet);
            }

            MemberTagging.MemberDetails member = dataAccess.LoadMemberForTagging(scanValue);
            if (member == null)
            {
                return Json(new { success = false, message = "Member Not Manufactured" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                memberCode = member.MemberCode,
                rfid = member.Rfid,
                sku = member.SkuCode
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult ValidateSticker(string stickerCode)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(stickerCode);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, isValid = false, message = "Please scan or enter QR Sticker." }, JsonRequestBehavior.AllowGet);
            }

            if (dataAccess.CheckMemberExists(scanValue))
            {
                return Json(new { success = true, isValid = false, message = "Member Already Exist." }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, isValid = true, stickerCode = scanValue }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> ReadPlateText()
        {
            try
            {
                OcrReadResult result = await QrOcrHelper.ReadGoogleVisionTextAsync(Request.InputStream, ConfigurationManager.AppSettings["GoogleVisionApiKey"]);
                if (!result.Success)
                {
                    return Json(new { success = false, message = result.Message, details = result.Details });
                }

                string memberPlate = ExtractMemberPlate(result.Text);
                if (string.IsNullOrWhiteSpace(memberPlate))
                {
                    return Json(new { success = false, message = "Member Plate not detected. Please try again or enter manually.", text = result.Text });
                }

                return Json(new { success = true, text = result.Text, memberPlate = memberPlate });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Unable to read member plate. Please try again or enter manually." });
            }
        }

        [HttpPost]
        public JsonResult Save(MemberTagging.TaggingRequest request)
        {
            string userCode = dataAccess.GetCurrentUserCode(this, "SYSTEM");
            MemberTagging.SaveResult result = dataAccess.SaveMemberTagging(request, userCode, userCode);

            return Json(new
            {
                success = result != null && result.Success,
                message = result == null ? "Unable to save member tagging." : result.Message,
                totalRecords = result == null ? 0 : result.TotalRecords
            });
        }

        private static string ExtractMemberPlate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string cleanedLine = CleanMemberPlateText(line);
                if (ContainsLetter(cleanedLine) && ContainsDigit(cleanedLine))
                {
                    return cleanedLine;
                }
            }

            return lines.Length > 0 ? CleanMemberPlateText(lines[0]) : string.Empty;
        }

        private static string CleanMemberPlateText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (char value in text.Trim())
            {
                if (char.IsLetterOrDigit(value) || value == '-' || value == '/' || value == '_')
                {
                    builder.Append(value);
                }
            }

            return QrOcrHelper.NormalizeQrScanValue(builder.ToString());
        }

        private static bool ContainsLetter(string text)
        {
            foreach (char value in text)
            {
                if (char.IsLetter(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDigit(string text)
        {
            foreach (char value in text)
            {
                if (char.IsDigit(value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}