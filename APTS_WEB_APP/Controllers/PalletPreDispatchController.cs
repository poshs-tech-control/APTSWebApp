using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class PalletPreDispatchController : Controller
    {
        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult PalletPreDispatch()
        {
            return View();
        }

        [HttpGet]
        public JsonResult ValidateMember(string rfid)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(rfid);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan member." }, JsonRequestBehavior.AllowGet);
            }

            PalletPreDispatch.PreDispatchValidationResult result = dataAccess.ValidatePreDispatchMember(scanValue);
            if (result == null)
            {
                return Json(new { success = false, message = "Unable to validate member." }, JsonRequestBehavior.AllowGet);
            }

            PalletPreDispatch.PalletOrderDimensions dimensions = result.IsAllowed
                ? dataAccess.LoadPalletOrderDimensionsByPalletCode(result.PalletCode)
                : null;

            return Json(new
            {
                success = result.IsAllowed,
                message = result.Message,
                palletCode = result.PalletCode,
                palletStatus = result.PalletStatus,
                palletCreatedDate = result.PalletCreatedDate,
                width = dimensions == null ? string.Empty : dimensions.Width,
                length1 = dimensions == null ? string.Empty : dimensions.Length1,
                length2 = dimensions == null ? string.Empty : dimensions.Length2
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult CheckExistingPreDispatch(string palletCode, string clientBatchNo)
        {
            if (string.IsNullOrWhiteSpace(palletCode) && string.IsNullOrWhiteSpace(clientBatchNo))
            {
                return Json(new { success = false, message = "Pallet Code or Client Batch No is required." }, JsonRequestBehavior.AllowGet);
            }

            PalletPreDispatch.ExistingPreDispatchItem existing = dataAccess.LoadExistingPreDispatch(
                string.IsNullOrWhiteSpace(palletCode) ? string.Empty : palletCode.Trim(),
                string.IsNullOrWhiteSpace(clientBatchNo) ? string.Empty : clientBatchNo.Trim());

            return Json(new
            {
                success = true,
                exists = existing != null,
                message = existing == null ? string.Empty : "Pallet Pre-Dispatch already exists.",
                item = existing
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult SavePreDispatch()
        {
            string palletCode = Convert.ToString(Request.Form["PalletCode"]);
            string clientBatchNo = Convert.ToString(Request.Form["ClientBatchNo"]);
            string sheetLength = Convert.ToString(Request.Form["SheetLengthH1"]);
            string width = Convert.ToString(Request.Form["SheetWidth"]);
            string weight = Convert.ToString(Request.Form["Weight"]);
            string thikness = Convert.ToString(Request.Form["Thickness"]);
            string storageLocation = Convert.ToString(Request.Form["StorageLocation"]);

            if (string.IsNullOrWhiteSpace(palletCode))
            {
                return Json(new { success = false, message = "Please validate member first." });
            }

            if (string.IsNullOrWhiteSpace(clientBatchNo))
            {
                return Json(new { success = false, message = "Please enter Client Batch No." });
            }

            if (string.IsNullOrWhiteSpace(storageLocation))
            {
                return Json(new { success = false, message = "Please enter Storage Location." });
            }

            string preDispatchCode = dataAccess.SavePreDispatchEntry(
                palletCode.Trim(),
                clientBatchNo.Trim(),
                string.IsNullOrWhiteSpace(sheetLength) ? "0" : sheetLength.Trim(),
                string.IsNullOrWhiteSpace(width) ? "0" : width.Trim(),
                string.IsNullOrWhiteSpace(weight) ? "0" : weight.Trim(),
                string.IsNullOrWhiteSpace(thikness) ? "0" : thikness.Trim(),
                storageLocation.Trim(),
                dataAccess.GetCurrentUserCode(this),
                dataAccess.GetIpAddress(this), dataAccess.GetCurrentUserFullName(this));

            if (string.IsNullOrWhiteSpace(preDispatchCode))
            {
                return Json(new { success = false, message = "Unable to save Pallet Pre-Dispatch." });
            }

            if (preDispatchCode.IndexOf("AVAILABLE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Json(new { success = false, message = preDispatchCode });
            }

            return Json(new
            {
                success = true,
                palletPreDispatchCode = preDispatchCode,
                message = "Pallet Pre-Dispatch Saved Successfully"
            });
        }

        [HttpPost]
        public async Task<ActionResult> ReadUploadText()
        {
            OcrReadResult result = await QrOcrHelper.ReadGoogleVisionTextAsync(Request.InputStream, ConfigurationManager.AppSettings["GoogleVisionApiKey"]);
            if (!result.Success)
            {
                return Json(new { success = false, message = result.Message, details = result.Details });
            }

            return Json(new
            {
                success = true,
                text = result.Text,
                fields = QrOcrHelper.ParsePreDispatchFields(result.Text)
            });
        }

    }
}
