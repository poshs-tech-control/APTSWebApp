using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class MemberCollectionController : Controller
    {
        private const string CacheKeyPrefix = "MemberCollection:";
        private static readonly TimeSpan DropdownCacheDuration = TimeSpan.FromMinutes(5);

        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult MemberCollection()
        {
            string userCode = dataAccess.GetCurrentUserCode(this);
            ViewBag.CustomerList = GetCachedDropdown(
                CacheKeyPrefix + "Customers:" + userCode,
                () => dataAccess.LoadMemberCollectionCustomerList(userCode));
            return View();
        }

        [HttpPost]
        public ActionResult MemberCollectionDetails(MemberCollection.MemberCollectionSetupInput request)
        {
            request = request ?? new MemberCollection.MemberCollectionSetupInput();
            request.Members = CleanMembers(request.Members);

            ViewBag.Setup = request;
            ViewBag.TransportList = GetCachedDropdown(
                CacheKeyPrefix + "Transports",
                () => dataAccess.LoadTransportListInMemColl());
            return View();
        }

        [HttpPost]
        public JsonResult ValidateCollectionMember(string tagRfid, string customerCode)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan or enter a member.", members = new object[0] });
            }

            if (string.IsNullOrWhiteSpace(customerCode))
            {
                return Json(new { success = false, message = "Please select Disassemble from.", members = new object[0] });
            }

            string rfid = dataAccess.LoadRfidByStickerMemCollection(scanValue);
            if (string.IsNullOrWhiteSpace(rfid))
            {
                return Json(new { success = false, message = "Member Not Tag / Member Not Found", members = new object[0] });
            }

            List<MemberCollection.CollectionMemberItem> members = dataAccess.LoadMemberCollectionItemsByRfid(rfid);
            if (!members.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Member status must be Pallet Disassemble from Customer or Pallet Disassemble from Customer-Damage.",
                    members = new object[0]
                });
            }

            if (members.Any(item => !string.IsNullOrWhiteSpace(item.CustCode)
                && !string.Equals(item.CustCode.Trim(), customerCode.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return Json(new { success = false, message = "Member does not belong to the selected customer.", members = new object[0] });
            }

            return Json(new
            {
                success = true,
                rfid = rfid,
                members = members,
                message = "Member validated successfully and added to the table."
            });
        }

        [HttpPost]
        public JsonResult SaveMemberCollection(MemberCollection.MemberCollectionSaveRequest request)
        {
            request = request ?? new MemberCollection.MemberCollectionSaveRequest();
            request.Members = CleanMembers(request.Members);

            if (string.IsNullOrWhiteSpace(request.FromLocation))
            {
                return Json(new { success = false, message = "Please select Disassemble from." });
            }

            if (string.IsNullOrWhiteSpace(request.TruckNo))
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

            if (string.IsNullOrWhiteSpace(request.TransportCode))
            {
                request.TransportCode = request.TransporterName;
            }

            if (!request.Members.Any())
            {
                return Json(new { success = false, message = "Please add at least one member." });
            }

            MemberCollection.MemberCollectionSaveResult result = dataAccess.SaveMemberCollection(
                request,
                dataAccess.GetCurrentUserCode(this),
                dataAccess.GetCurrentUserFullName(this),
                dataAccess.GetIpAddress(this));

            bool saved = result != null && result.Success;
            return Json(new
            {
                success = saved,
                collectionCode = result == null ? string.Empty : result.CollectionCode,
                totalRecords = result == null ? 0 : result.TotalRecords,
                challanPending = saved && !string.IsNullOrWhiteSpace(result.CollectionCode),
                message = result == null ? "Unable to save Member Collection." : result.Message
            });
        }

        private ChallanMailResult TryGenerateMemberCollectionChallan(string collectionCode)
        {
            if (string.IsNullOrWhiteSpace(collectionCode))
            {
                return null;
            }

            try
            {
                return new ChallanGenerationService(dataAccess, Server).GenerateMemberCollection(collectionCode);
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
        private static List<T> GetCachedDropdown<T>(string cacheKey, Func<List<T>> loader)
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
                DateTime.UtcNow.Add(DropdownCacheDuration),
                System.Web.Caching.Cache.NoSlidingExpiration);
            return items;
        }
        private static List<MemberCollection.CollectionMemberItem> CleanMembers(IEnumerable<MemberCollection.CollectionMemberItem> members)
        {
            return (members ?? Enumerable.Empty<MemberCollection.CollectionMemberItem>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.RFIDNo))
                .GroupBy(item => item.RFIDNo.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }
    }
}

