using System;
using System.Collections.Generic;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class InternalTransferController : Controller
    {
        private const string FactoryLocationType = "MISC0000019";
        private const string WarehouseLocationType = "MISC0000020";

        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult InternalTransfer()
        {
            string userCode = dataAccess.GetCurrentUserCode(this);

            ViewBag.FactoryLocations = dataAccess.LoadInternalTransferLocationList(FactoryLocationType, userCode);
            ViewBag.WarehouseLocations = dataAccess.LoadInternalTransferLocationList(WarehouseLocationType, userCode);
            ViewBag.ClientLocations = dataAccess.LoadInternalTransferCustomerList(userCode);
            ViewBag.SessionLocationType = Convert.ToString(Session["LOCATIONTYPE"]);
            ViewBag.SessionLocationCode = Convert.ToString(Session["LocationCode"]);
            ViewBag.SessionLocationTitle = Convert.ToString(Session["LocationTitle"]);
            return View();
        }

        [HttpPost]
        public ActionResult MemberDispatch(InternalTransfer.DispatchSetupInput request)
        {
            request = ApplySessionFromLocation(request ?? new InternalTransfer.DispatchSetupInput());
            ViewBag.Setup = request;
            ViewBag.TruckList = LoadInternalTransferTruckList();
            return View();
        }

        [HttpPost]
        public JsonResult ValidateTransferMember(string tagRfid, string currentLocation)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan member." });
            }

            if (string.IsNullOrWhiteSpace(currentLocation))
            {
                return Json(new { success = false, message = "Please select location." });
            }

            Models.InternalTransfer.MemberValidationResult result =
                dataAccess.ValidateInternalTransferMember(scanValue, currentLocation.Trim());

            return Json(new
            {
                success = result != null && result.IsValid,
                rfid = result == null ? string.Empty : result.Rfid,
                memberType = result == null ? string.Empty : result.MemberType,
                memberSize = result == null ? string.Empty : result.MemberSize,
                memberCode = result == null ? string.Empty : result.MemberCode,
                memberWeight = result == null ? string.Empty : result.MemberWeight,
                memberDimension = result == null ? string.Empty : result.MemberDimension,
                active = result == null ? string.Empty : result.Active,
                message = result == null ? "Unable to validate member." : result.Message
            });
        }

        [HttpPost]
        public JsonResult SubmitMemberDispatch(InternalTransfer.DispatchSaveRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Internal Transfer details are required." });
            }

            request = (InternalTransfer.DispatchSaveRequest)ApplySessionFromLocation(request);

            if (string.IsNullOrWhiteSpace(request.UserLocation)
                || string.IsNullOrWhiteSpace(request.ToLocation)
                || string.IsNullOrWhiteSpace(request.UserLocationType)
                || string.IsNullOrWhiteSpace(request.ToLocationType))
            {
                return Json(new { success = false, message = "Please select transfer locations." });
            }

            if (string.Equals(request.UserLocationType, request.ToLocationType, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.UserLocation, request.ToLocation, System.StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "From and To location cannot be same." });
            }

            if (request.Members == null || request.Members.Count == 0)
            {
                return Json(new { success = false, message = "Please select member." });
            }

            if (string.IsNullOrWhiteSpace(request.TruckNumber))
            {
                return Json(new { success = false, message = "Please enter Truck Number." });
            }

            if (string.IsNullOrWhiteSpace(request.DriverName))
            {
                return Json(new { success = false, message = "Please enter Driver Name." });
            }

            if (string.IsNullOrWhiteSpace(request.DriverContactNo))
            {
                return Json(new { success = false, message = "Please enter Driver Contact No." });
            }

            if (string.IsNullOrWhiteSpace(request.TransporterName))
            {
                return Json(new { success = false, message = "Please enter Transporter Name." });
            }

            InternalTransfer.DispatchSaveResult result = dataAccess.SaveInternalTransferDispatch(
                request,
                dataAccess.GetCurrentUserCode(this),
                dataAccess.GetIpAddress(this));

            bool saved = result != null && result.Success;
            return Json(new
            {
                success = saved,
                matDispCode = result == null ? string.Empty : result.MatDispCode,
                totalRecords = result == null ? 0 : result.TotalRecords,
                challanPending = saved && request.GenerateChallan && !string.IsNullOrWhiteSpace(result.MatDispCode),
                message = result == null ? "Unable to save Internal Transfer." : result.Message
            });
        }

        private ChallanMailResult TryGenerateMemberDispatchChallan(bool generateChallan, string matDispCode)
        {
            if (!generateChallan || string.IsNullOrWhiteSpace(matDispCode))
            {
                return null;
            }

            try
            {
                return new ChallanGenerationService(dataAccess, Server).GenerateMemberDispatch(matDispCode);
            }
            catch (Exception ex)
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
        private InternalTransfer.DispatchSetupInput ApplySessionFromLocation(InternalTransfer.DispatchSetupInput request)
        {
            if (request == null)
            {
                request = new InternalTransfer.DispatchSetupInput();
            }

            string sessionLocationType = Convert.ToString(Session["LOCATIONTYPE"]);
            string sessionLocationCode = Convert.ToString(Session["LocationCode"]);

            if (!string.IsNullOrWhiteSpace(sessionLocationType))
            {
                request.UserLocationType = sessionLocationType;
            }

            if (!string.IsNullOrWhiteSpace(sessionLocationCode))
            {
                request.UserLocation = sessionLocationCode;
            }

            return request;
        }
        private List<InternalTransfer.TransportItem> LoadInternalTransferTruckList()
        {
            return dataAccess.LoadTransportList() ?? new List<InternalTransfer.TransportItem>();
        }
    }
}


