using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class PalletDisassembleClientController : Controller
    {
        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult PalletDisassembleClient()
        {
            return View();
        }

        [HttpPost]
        public JsonResult ValidateDisassembleMember(string tagRfid)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan member." });
            }

            PalletDisassembleClient.ValidationResult result = dataAccess.ValidatePalletDisassembleClientMember(
                scanValue,
                dataAccess.GetCurrentUserCode(this));

            return Json(new
            {
                success = result != null && result.Success,
                rfid = result == null ? string.Empty : result.Rfid,
                palletCode = result == null ? string.Empty : result.PalletCode,
                message = result == null ? "Unable to validate member." : result.Message,
                members = result == null ? new System.Collections.Generic.List<PalletDisassembleClient.DisassembleMemberItem>() : result.Members
            });
        }

        [HttpPost]
        public JsonResult SaveDisassemble(PalletDisassembleClient.SaveRequest request)
        {
            request = request ?? new PalletDisassembleClient.SaveRequest();
            request.Members = request.Members ?? new System.Collections.Generic.List<PalletDisassembleClient.DisassembleMemberInput>();

            if (request.Members.Count == 0)
            {
                return Json(new { success = false, message = "Please select member." });
            }

            PalletDisassembleClient.SaveResult result = dataAccess.SavePalletDisassembleClient(
                request,
                dataAccess.GetCurrentUserCode(this),
                dataAccess.GetCurrentUserFullName(this),
                dataAccess.GetIpAddress(this));

            return Json(new
            {
                success = result != null && result.Success,
                dismentleCode = result == null ? string.Empty : result.DismentleCode,
                detailCount = result == null ? 0 : result.DetailCount,
                message = result == null ? "Unable to save Pallet Disassemble." : result.Message
            });
        }
    }
}
