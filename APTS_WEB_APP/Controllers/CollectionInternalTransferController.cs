using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class CollectionInternalTransferController : Controller
    {
        private const string FactoryLocationType = "MISC0000019";
        private const string WarehouseLocationType = "MISC0000020";
        private const string CacheKeyPrefix = "CollectionInternalTransfer:";
        private static readonly System.TimeSpan DropdownCacheDuration = System.TimeSpan.FromMinutes(5);

        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult CollectionInternalTransfer()
        {
            string userCode = dataAccess.GetCurrentUserCode(this);

            ViewBag.TruckList = GetCachedDropdown(
                CacheKeyPrefix + "Trucks",
                () => dataAccess.LoadTransportList());
            ViewBag.FactoryLocations = GetCachedDropdown(
                CacheKeyPrefix + "Factories:" + userCode,
                () => dataAccess.LoadInternalTransferLocationList(FactoryLocationType, userCode));
            ViewBag.WarehouseLocations = GetCachedDropdown(
                CacheKeyPrefix + "Warehouses:" + userCode,
                () => dataAccess.LoadInternalTransferLocationList(WarehouseLocationType, userCode));
            ViewBag.ClientLocations = GetCachedDropdown(
                CacheKeyPrefix + "Clients:" + userCode,
                () => dataAccess.LoadInternalTransferCustomerList(userCode));
            return View();
        }

        private static List<T> GetCachedDropdown<T>(string cacheKey, System.Func<List<T>> loader)
        {
            var cachedItems = HttpRuntime.Cache[cacheKey] as List<T>;
            if (cachedItems != null)
            {
                return cachedItems;
            }

            List<T> items = loader() ?? new List<T>();
            HttpRuntime.Cache.Insert(
                cacheKey,
                items,
                null,
                System.DateTime.UtcNow.Add(DropdownCacheDuration),
                System.Web.Caching.Cache.NoSlidingExpiration);
            return items;
        }

        [HttpPost]
        public ActionResult CollectionInternalTransferDetails(InternalTransfer.DispatchSetupInput request, string truckNo, string driverName, string driverContactNo, string transporterName)
        {
            request = request ?? new InternalTransfer.DispatchSetupInput();
            request.UserLocationType = "Collection Truck";
            request.UserLocation = string.IsNullOrWhiteSpace(request.UserLocation) ? truckNo : request.UserLocation;

            if (!string.IsNullOrWhiteSpace(truckNo)
                && (string.IsNullOrWhiteSpace(driverName)
                    || string.IsNullOrWhiteSpace(driverContactNo)
                    || string.IsNullOrWhiteSpace(transporterName)))
            {
                InternalTransfer.TransportItem collectionTruck = dataAccess.LoadMemberCollectionTruckDetails(truckNo);
                if (collectionTruck != null)
                {
                    driverName = string.IsNullOrWhiteSpace(driverName) ? collectionTruck.DriverName : driverName;
                    driverContactNo = string.IsNullOrWhiteSpace(driverContactNo) ? collectionTruck.MobileNo : driverContactNo;
                    transporterName = string.IsNullOrWhiteSpace(transporterName) ? collectionTruck.TransporterName : transporterName;
                }
            }

            ViewBag.Setup = request;
            ViewBag.TruckNo = truckNo;
            ViewBag.DriverName = driverName;
            ViewBag.DriverContactNo = driverContactNo;
            ViewBag.TransporterName = transporterName;
            return View();
        }
        [HttpPost]
        public JsonResult ValidateCollectionTransferMember(string tagRfid, string selectedTruckCode, string selectedTruckNo)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan member." });
            }

            InternalTransfer.CollectionBulkValidationResult result = dataAccess.ValidateCollectionInternalTransferMember(scanValue, selectedTruckCode, selectedTruckNo);
            return Json(new
            {
                success = result != null && result.IsValid,
                bypassRequired = result != null && result.BypassRequired,
                rfid = result == null ? string.Empty : result.Rfid,
                members = result == null || result.Members == null
                    ? new object[0]
                    : result.Members.Select(member => new
                    {
                        rfid = member.Rfid,
                        tagRfid = member.TagRfid,
                        memberType = member.MemberType,
                        memberSize = member.MemberSize,
                        memberCode = member.MemberCode,
                        memberWeight = member.MemberWeight,
                        memberDimension = member.MemberDimension
                    }).ToArray(),
                message = result == null ? "Unable to validate member." : result.Message
            });
        }

        [HttpPost]
        public JsonResult ContinueCollectionTransferBypass(string rfid, string selectedTruckCode, string selectedTruckNo)
        {
            string userName = dataAccess.GetCurrentUserFullName(this);
            InternalTransfer.CollectionBulkValidationResult result = dataAccess.ContinueCollectionInternalTransferBypass(
                QrOcrHelper.NormalizeQrScanValue(rfid),
                selectedTruckCode,
                selectedTruckNo,
                userName);

            return Json(new
            {
                success = result != null && result.IsValid,
                rfid = result == null ? string.Empty : result.Rfid,
                members = result == null || result.Members == null
                    ? new object[0]
                    : result.Members.Select(member => new
                    {
                        rfid = member.Rfid,
                        tagRfid = member.TagRfid,
                        memberType = member.MemberType,
                        memberSize = member.MemberSize,
                        memberCode = member.MemberCode,
                        memberWeight = member.MemberWeight,
                        memberDimension = member.MemberDimension
                    }).ToArray(),
                message = result == null ? "Unable to continue member bypass." : result.Message
            });
        }

        [HttpPost]
        public JsonResult SubmitCollectionInternalTransfer(InternalTransfer.DispatchSaveRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Collection Internal Transfer details are required." });
            }

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
                return Json(new { success = false, message = "Truck Number is required." });
            }


            InternalTransfer.DispatchSaveResult result = dataAccess.SaveCollectionInternalTransferDispatch(
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
                message = result == null ? "Unable to save Collection Internal Transfer." : result.Message
            });
        }

        private ChallanMailResult TryGenerateCollectionInternalTransferChallan(bool generateChallan, string matDispCode)
        {
            if (!generateChallan || string.IsNullOrWhiteSpace(matDispCode))
            {
                return null;
            }

            try
            {
                return new ChallanGenerationService(dataAccess, Server).GenerateMemberDispatch(matDispCode);
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



