using System;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class MemberReceiptController : Controller
    {
        private const string FactoryLocationType = "MISC0000019";
        private const string WarehouseLocationType = "MISC0000020";
        private const string ClientLocationType = "MISC0000072";

        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult MemberReceipt()
        {
            return RedirectToAction("MemberReceiptScan");
        }

        public ActionResult MemberReceiptScan(MemberReceipt.ReceiptSetupInput request)
        {
            request = ApplySessionReceiptLocation(request ?? new MemberReceipt.ReceiptSetupInput());
            ViewBag.ReceiptFrom = request.ReceiptFrom;
            ViewBag.ReceiptFromTitle = GetReceiptLocationTypeTitle(request.ReceiptFrom);
            ViewBag.ReceiptLocation = request.ReceiptLocation;
            ViewBag.ReceiptLocationName = request.ReceiptLocationName;
            return View();
        }

        [HttpPost]
        public JsonResult ValidateReceiptMember(string tagRfid)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan member." });
            }

            MemberReceipt.ReceiveValidationResult result = dataAccess.ValidateMemberReceiptMember(scanValue, GetSessionReceiptLocationTitle());

            return Json(new
            {
                success = result != null && result.Success,
                rfid = result == null ? string.Empty : result.Rfid,
                message = result == null ? "Unable to validate member." : result.Message,
                members = result == null ? new System.Collections.Generic.List<MemberReceipt.ReceiveMemberItem>() : result.Members
            });
        }

        [HttpPost]
        public JsonResult SaveReceipt(MemberReceipt.ReceiveSaveRequest request)
        {
            request = ApplySessionReceiptLocation(request ?? new MemberReceipt.ReceiveSaveRequest());
            request.Members = request.Members ?? new System.Collections.Generic.List<MemberReceipt.ReceiveMemberInput>();

            if (string.IsNullOrWhiteSpace(request.ReceiptFrom)
                || string.IsNullOrWhiteSpace(request.ReceiptLocation))
            {
                return Json(new { success = false, message = "Please select receipt location." });
            }

            if (request.Members.Count == 0)
            {
                return Json(new { success = false, message = "Please select member." });
            }

            MemberReceipt.ReceiveSaveResult result = dataAccess.SaveMemberReceipt(
                request,
                dataAccess.GetCurrentUserCode(this),
                dataAccess.GetCurrentUserFullName(this),
                dataAccess.GetIpAddress(this));

            return Json(new
            {
                success = result != null && result.Success,
                materialRecCode = result == null ? string.Empty : result.MaterialRecCode,
                detailCount = result == null ? 0 : result.DetailCount,
                message = result == null ? "Unable to save Member Receipt." : result.Message
            });
        }

        private MemberReceipt.ReceiptSetupInput ApplySessionReceiptLocation(MemberReceipt.ReceiptSetupInput request)
        {
            if (request == null)
            {
                request = new MemberReceipt.ReceiptSetupInput();
            }

            ApplySessionReceiptLocationValues(
                value => request.ReceiptFrom = value,
                value => request.ReceiptLocation = value,
                value => request.ReceiptLocationName = value);

            return request;
        }

        private MemberReceipt.ReceiveSaveRequest ApplySessionReceiptLocation(MemberReceipt.ReceiveSaveRequest request)
        {
            if (request == null)
            {
                request = new MemberReceipt.ReceiveSaveRequest();
            }

            ApplySessionReceiptLocationValues(
                value => request.ReceiptFrom = value,
                value => request.ReceiptLocation = value,
                value => request.ReceiptLocationName = value);

            return request;
        }

        private string GetSessionReceiptLocationTitle()
        {
            string sessionLocationTitle = Convert.ToString(Session["FromLocationTitle"]);
            if (string.IsNullOrWhiteSpace(sessionLocationTitle))
            {
                sessionLocationTitle = Convert.ToString(Session["LocationTitle"]);
            }

            return sessionLocationTitle;
        }

        private void ApplySessionReceiptLocationValues(Action<string> setLocationType, Action<string> setLocationCode, Action<string> setLocationTitle)
        {
            string sessionLocationType = Convert.ToString(Session["LOCATIONTYPE"]);
            string sessionLocationCode = Convert.ToString(Session["LocationCode"]);
            string sessionLocationTitle = Convert.ToString(Session["FromLocationTitle"]);

            if (string.IsNullOrWhiteSpace(sessionLocationTitle))
            {
                sessionLocationTitle = Convert.ToString(Session["LocationTitle"]);
            }

            if (!string.IsNullOrWhiteSpace(sessionLocationType))
            {
                setLocationType(sessionLocationType);
            }

            if (!string.IsNullOrWhiteSpace(sessionLocationCode))
            {
                setLocationCode(sessionLocationCode);
            }

            if (!string.IsNullOrWhiteSpace(sessionLocationTitle))
            {
                setLocationTitle(sessionLocationTitle);
            }
        }

        private static string GetReceiptLocationTypeTitle(string locationType)
        {
            if (string.Equals(locationType, FactoryLocationType, StringComparison.OrdinalIgnoreCase))
            {
                return "Factory";
            }

            if (string.Equals(locationType, WarehouseLocationType, StringComparison.OrdinalIgnoreCase))
            {
                return "Warehouse";
            }

            if (string.Equals(locationType, ClientLocationType, StringComparison.OrdinalIgnoreCase))
            {
                return "Customer";
            }

            return string.IsNullOrWhiteSpace(locationType) ? "Location" : locationType;
        }
    }
}