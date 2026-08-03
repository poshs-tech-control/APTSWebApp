using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class PalletDisassembleCustomerController : Controller
    {
        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult PalletDisassembleCustomer()
        {
            string userCode = dataAccess.GetCurrentUserCode(this);
            dataAccess.ClearPalletDisassembleCustomerTempForUser(userCode);
            ViewBag.ToCustomerList = dataAccess.LoadShipToLocationList(userCode);
            return View();
        }

        [HttpPost]
        public JsonResult ValidateDisassembleCustomerMember(string currentLocation, string tagRfid)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan member." });
            }

            PalletDisassembleCustomer.ValidationResult result =
                dataAccess.ValidatePalletDisassembleCustomerMember(currentLocation, scanValue);

            return Json(new
            {
                success = result != null && result.Success,
                rfid = result == null ? string.Empty : result.Rfid,
                palletCode = result == null ? string.Empty : result.PalletCode,
                message = result == null ? "Unable to validate member." : result.Message,
                members = result == null ? new System.Collections.Generic.List<PalletDisassembleCustomer.DispatchDetailItem>() : result.Members
            });
        }

        [HttpPost]
        public JsonResult ClearDisassembleCustomerTemp()
        {
            bool cleared = dataAccess.ClearPalletDisassembleCustomerTempForUser(dataAccess.GetCurrentUserCode(this));
            return Json(new { success = cleared });
        }
        [HttpPost]
        public JsonResult SaveDisassembleCustomerTemp(PalletDisassembleCustomer.TempSaveRequest request)
        {
            request = request ?? new PalletDisassembleCustomer.TempSaveRequest();
            request.Members = request.Members ?? new System.Collections.Generic.List<PalletDisassembleCustomer.TempSaveMemberInput>();

            if (request.Members.Count == 0)
            {
                return Json(new { success = false, message = "Please select member.", totalRecords = 0 });
            }

            PalletDisassembleCustomer.TempSaveResult result = dataAccess.SavePalletDisassembleCustomerTemp(
                request,
                dataAccess.GetCurrentUserCode(this));

            return Json(new
            {
                success = result != null && result.Success,
                message = result == null ? "Unable to save Pallet Dismantle Customer." : result.Message,
                totalRecords = result == null ? 0 : result.TotalRecords
            });
        }

        [HttpPost]
        public JsonResult GetDisassembleCustomerTempDetails(string custDismentleCode)
        {
            if (string.IsNullOrWhiteSpace(custDismentleCode))
            {
                return Json(new { success = false, message = "Disassemble code not found.", items = new System.Collections.Generic.List<PalletDisassembleCustomer.TempDetailItem>() });
            }

            System.Collections.Generic.List<PalletDisassembleCustomer.TempDetailItem> items =
                dataAccess.LoadPalletDisassembleCustomerTempDetails(custDismentleCode);

            return Json(new
            {
                success = items.Count > 0,
                message = items.Count > 0 ? "Details loaded." : "Details not found.",
                items = items
            });
        }

        [HttpPost]
        public JsonResult SubmitDisassembleCustomer(PalletDisassembleCustomer.FinalSaveRequest request)
        {
            request = request ?? new PalletDisassembleCustomer.FinalSaveRequest();
            request.Groups = request.Groups ?? new System.Collections.Generic.List<PalletDisassembleCustomer.FinalSaveGroupInput>();

            if (string.IsNullOrWhiteSpace(request.CustomerCode))
            {
                return Json(new { success = false, message = "Please select To Customer.", custDismentleCode = string.Empty, totalRecords = 0 });
            }

            if (request.Groups.Count == 0)
            {
                return Json(new { success = false, message = "Please disassemble pallet first.", custDismentleCode = string.Empty, totalRecords = 0 });
            }

            PalletDisassembleCustomer.FinalSaveResult result = dataAccess.SavePalletDisassembleCustomerFinal(
                request,
                dataAccess.GetCurrentUserCode(this),
                dataAccess.GetIpAddress(this));

            bool saved = result != null && result.Success;
            return Json(new
            {
                success = saved,
                message = result == null ? "Unable to save Pallet Dismantle Customer." : result.Message,
                custDismentleCode = result == null ? string.Empty : result.CustDismentleCode,
                totalRecords = result == null ? 0 : result.TotalRecords,
                challanPending = saved && !string.IsNullOrWhiteSpace(result.CustDismentleCode)
            });
        }
        private ChallanMailResult TryGeneratePalletDismentleChallan(string custDismentleCode)
        {
            if (string.IsNullOrWhiteSpace(custDismentleCode))
            {
                return null;
            }

            try
            {
                return new ChallanGenerationService(dataAccess, Server).GeneratePalletDismentle(custDismentleCode);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return new ChallanMailResult { Success = false, Message = "Challan not generated: " + ex.Message };
            }
        }

        private static string BuildSubmitMessage(string saveMessage, ChallanMailResult challanResult)
        {
            if (challanResult == null)
            {
                return saveMessage;
            }

            return challanResult.Success
                ? saveMessage + " and Challan Mail Send"
                : saveMessage + " but " + challanResult.Message;
        }
    }
}



