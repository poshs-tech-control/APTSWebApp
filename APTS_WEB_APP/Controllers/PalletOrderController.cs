using System;
using System.Collections.Generic;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class PalletOrderController : Controller
    {
        private const string FactoryLocationType = "MISC0000019";
        private const string WarehouseLocationType = "MISC0000020";
        private const string ClientLocationType = "MISC0000072";

        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult PalletOrder()
        {
            ViewBag.PalletShapeList = dataAccess.LoadPalletShapeList();
            ViewBag.SessionLocationCode = Convert.ToString(Session["LocationCode"]);
            ViewBag.SessionLocationTitle = GetSessionLocationTitle();
            ViewBag.SessionLocationTypeTitle = GetLocationTypeTitle(Convert.ToString(Session["LOCATIONTYPE"]));
            ViewBag.SessionCustomerCode = Convert.ToString(Session["CustomerCode"]);
            return View();
        }

        public ActionResult PalletOrderDetails()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetMatchingSkus(string shape, string width, string length)
        {
            if (string.IsNullOrWhiteSpace(shape)
                || string.IsNullOrWhiteSpace(width)
                || string.IsNullOrWhiteSpace(length))
            {
                return Json(new { success = false, message = "Enter pallet shape, width and length." }, JsonRequestBehavior.AllowGet);
            }

            List<PalletOrder.PalletSkuItem> skuList = dataAccess.LoadPalletSkuList(shape.Trim(), length.Trim(), width.Trim());
            return Json(new { success = true, skus = skuList }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult SubmitPalletOrder()
        {
            string locationCode = Convert.ToString(Session["LocationCode"]);
            string customerCode = Convert.ToString(Session["CustomerCode"]);
            if (string.IsNullOrWhiteSpace(locationCode))
            {
                return Json(new { success = false, message = "Location is not found in session." });
            }

            if (string.IsNullOrWhiteSpace(customerCode))
            {
                return Json(new { success = false, message = "Customer is not found in session." });
            }

            List<PalletOrder.PalletOrderLineItem> lineItems = BuildPalletOrderLines();
            if (lineItems.Count == 0)
            {
                return Json(new { success = false, message = "Please add at least one line item." });
            }



            PalletOrder.PalletOrderSaveResult result = dataAccess.SavePalletOrder(
                customerCode.Trim(),
                locationCode.Trim(),
                lineItems,
                dataAccess.GetCurrentUserCode(this),
                dataAccess.GetIpAddress(this));
            if (result == null || string.IsNullOrWhiteSpace(result.PalletOrderCode))
            {
                return Json(new { success = false, message = result != null && !string.IsNullOrWhiteSpace(result.Message) ? result.Message : "Unable to submit pallet order." });
            }

            if (result.DetailCount <= 0)
            {
                return Json(new { success = false, message = !string.IsNullOrWhiteSpace(result.Message) ? result.Message : "Unable to submit pallet order details." });
            }

            return Json(new
            {
                success = true,
                palletOrderCode = result.PalletOrderCode,
                detailCount = result.DetailCount,
                challanPending = !string.IsNullOrWhiteSpace(result.PalletOrderCode),
                message = "Pallet Order Submitted Successfully"
            });
        }

        private ChallanMailResult TryGeneratePalletOrderChallan(string palletOrderCode)
        {
            if (string.IsNullOrWhiteSpace(palletOrderCode))
            {
                return null;
            }

            try
            {
                return new ChallanGenerationService(dataAccess, Server).GeneratePalletOrder(palletOrderCode);
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
        private List<PalletOrder.PalletOrderLineItem> BuildPalletOrderLines()
        {
            var items = new List<PalletOrder.PalletOrderLineItem>();
            string[] skuCodes = Request.Form.GetValues("OrderSkuCode") ?? new string[0];
            string[] shapes = Request.Form.GetValues("OrderShape") ?? new string[0];
            string[] widths = Request.Form.GetValues("OrderWidth") ?? new string[0];
            string[] length1Values = Request.Form.GetValues("OrderLength1") ?? new string[0];
            string[] length2Values = Request.Form.GetValues("OrderLength2") ?? new string[0];
            string[] qtyValues = Request.Form.GetValues("OrderQty") ?? new string[0];
            string[] instructions = Request.Form.GetValues("OrderInstruction") ?? new string[0];

            for (int i = 0; i < skuCodes.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(skuCodes[i]))
                {
                    continue;
                }

                items.Add(new PalletOrder.PalletOrderLineItem
                {
                    SkuCode = skuCodes[i].Trim(),
                    Shape = GetArrayValue(shapes, i),
                    Width = GetArrayValue(widths, i),
                    Length1 = GetArrayValue(length1Values, i),
                    Length2 = string.IsNullOrWhiteSpace(GetArrayValue(length2Values, i)) ? "0" : GetArrayValue(length2Values, i),
                    Qty = GetArrayValue(qtyValues, i),
                    Instruction = GetArrayValue(instructions, i)
                });
            }

            return items;
        }

        private static string GetArrayValue(string[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length && values[index] != null
                ? values[index].Trim()
                : string.Empty;
        }

        private string GetSessionLocationTitle()
        {
            string locationTitle = Convert.ToString(Session["FromLocationTitle"]);
            if (string.IsNullOrWhiteSpace(locationTitle))
            {
                locationTitle = Convert.ToString(Session["LocationTitle"]);
            }

            return locationTitle;
        }

        private static string GetLocationTypeTitle(string locationType)
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
                return "Client";
            }

            return string.IsNullOrWhiteSpace(locationType) ? "Location" : locationType;
        }
    }
}

