using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;
using Newtonsoft.Json.Linq;

namespace APTS_WEB_APP.Controllers
{
    public class PalletDispatchController : Controller
    {
        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult PalletDispatch()
        {
            string userCode = dataAccess.GetCurrentUserCode(this);
            ApplySessionFromLocationViewBag();
            ViewBag.ToCustomerList = dataAccess.LoadShipToLocationList(userCode);
            return View();
        }

        [HttpPost]
        public ActionResult PalletDispatchDetails(PalletDispatcch.PalletDispatchDetailsInput input)
        {
            input = ApplySessionFromLocation(input ?? new PalletDispatcch.PalletDispatchDetailsInput());
            input.DispatchItems = input.DispatchItems ?? new List<PalletDispatcch.DispatchItemInput>();
            return View(input);
        }

        [HttpPost]
        public JsonResult SaveDispatch(PalletDispatcch.PalletDispatchSaveRequest request)
        {
            request = ApplySessionFromLocation(request ?? new PalletDispatcch.PalletDispatchSaveRequest());
            request.DispatchItems = request.DispatchItems ?? new List<PalletDispatcch.DispatchItemInput>();
            request.GenerateChallan = true;

            if (string.IsNullOrWhiteSpace(request.FromLocation))
            {
                return Json(new { success = false, message = "Session From Location not found. Please login again." });
            }

            if (string.IsNullOrWhiteSpace(request.ShipToLocation))
            {
                return Json(new { success = false, message = "Please select To Customer." });
            }

            if (string.IsNullOrWhiteSpace(request.Consignee))
            {
                return Json(new { success = false, message = "Please select Consignee." });
            }

            List<PalletDispatcch.DispatchItemInput> dispatchItems = request.DispatchItems
                .Where(item => item != null
                    && !string.IsNullOrWhiteSpace(item.PalletCode)
                    && !string.Equals(item.PalletCode, "-", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(item.ClientBatchNo)
                    && !string.Equals(item.ClientBatchNo, "-", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!dispatchItems.Any())
            {
                return Json(new { success = false, message = "Please add dispatch batches." });
            }

            if (dataAccess.HasAlreadyDispatchedMembers(dispatchItems.Select(item => item.PalletCode)))
            {
                return Json(new { success = false, message = "Already Dispatch" });
            }

            string palletDispCode = dataAccess.SaveDispatchMaster(CreateDispatchMasterInput(request), dataAccess.GetCurrentUserCode(this), dataAccess.GetIpAddress(this, "MobApp"));
            if (string.IsNullOrWhiteSpace(palletDispCode))
            {
                return Json(new { success = false, message = "Unable to save Pallet Dispatch." });
            }

            PalletDispatcch.BulkDispatchSaveResult detailResult = dataAccess.SaveDispatchDetails(
                palletDispCode,
                CreateDispatchMasterInput(request),
                dispatchItems.Select(item => new PalletDispatcch.DispatchDetailInput
                {
                    PalletCode = item.PalletCode,
                    ClientBatchNo = item.ClientBatchNo
                }),
                dataAccess.GetCurrentUserCode(this));
            if (!detailResult.Success)
            {
                return Json(new { success = false, message = detailResult.Message });
            }

            bool challanPending = request.GenerateChallan && !string.IsNullOrWhiteSpace(palletDispCode);

            return Json(new
            {
                success = true,
                palletDispCode = palletDispCode,
                totalRecords = detailResult.TotalRecords,
                challanPending = challanPending,
                challanGenerated = false,
                challanMessage = string.Empty,
                message = "Pallet Dispatch Saved Successfully"
            });
        }

        [HttpPost]
        public JsonResult ValidateDispatchScan(string scanType, string scanValue)
        {
            string value = QrOcrHelper.NormalizeQrScanValue(scanValue);

            if (string.IsNullOrWhiteSpace(value))
            {
                return Json(new
                {
                    success = false,
                    message = "Members Not in PreDispatch."
                });
            }

            PalletDispatcch.DispatchValidationResult result = dataAccess.ValidateDispatchMember(value);

            return Json(new
            {
                success = result.IsValid,
                message = result.Message,
                lastStatus = result.LastStatus,
                item = result.Item
            });
        }

        [HttpPost]
        public JsonResult GetDispatchBatches(string clientBatchNos)
        {
            List<string> batches = ParseClientBatchList(clientBatchNos);
            if (!batches.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Some Members are not In PreDispatch.",
                    items = new object[0]
                });
            }

            List<PalletDispatcch.DispatchBatchItem> items = dataAccess.LoadDispatchBatches(batches);
            return Json(new
            {
                success = items.Any(),
                message = items.Any()
                    ? items.Count + " batch(es) loaded."
                    : "Some Members are not In PreDispatch.",
                items = items
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
                fields = QrOcrHelper.ParseDispatchFields(result.Text)
            });
        }


        private PalletDispatcch.PalletDispatchDetailsInput ApplySessionFromLocation(PalletDispatcch.PalletDispatchDetailsInput input)
        {
            if (input == null)
            {
                input = new PalletDispatcch.PalletDispatchDetailsInput();
            }

            input.FromLocation = GetSessionFromLocationCode();
            return input;
        }

        private PalletDispatcch.PalletDispatchSaveRequest ApplySessionFromLocation(PalletDispatcch.PalletDispatchSaveRequest request)
        {
            if (request == null)
            {
                request = new PalletDispatcch.PalletDispatchSaveRequest();
            }

            request.FromLocation = GetSessionFromLocationCode();
            return request;
        }

        private void ApplySessionFromLocationViewBag()
        {
            ViewBag.FromLocationCode = GetSessionFromLocationCode();
            ViewBag.FromLocationTitle = GetSessionFromLocationTitle();
        }

        private string GetSessionFromLocationCode()
        {
            string sessionLocationCode = Convert.ToString(Session["LocationCode"]);
            if (string.IsNullOrWhiteSpace(sessionLocationCode))
            {
                sessionLocationCode = Convert.ToString(Session["LOCATIONCODE"]);
            }

            return sessionLocationCode;
        }

        private string GetSessionFromLocationTitle()
        {
            string sessionLocationTitle = Convert.ToString(Session["FromLocationTitle"]);
            if (string.IsNullOrWhiteSpace(sessionLocationTitle))
            {
                sessionLocationTitle = Convert.ToString(Session["LocationTitle"]);
            }

            if (string.IsNullOrWhiteSpace(sessionLocationTitle))
            {
                sessionLocationTitle = Convert.ToString(Session["LOCATIONTITLE"]);
            }

            return sessionLocationTitle;
        }
        private ChallanMailResult TryGeneratePalletDispatchChallan(bool generateChallan, string palletDispCode)
        {
            if (!generateChallan || string.IsNullOrWhiteSpace(palletDispCode))
            {
                return null;
            }

            try
            {
                return new ChallanGenerationService(dataAccess, Server).GeneratePalletDispatch(palletDispCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return new ChallanMailResult { Success = false, Message = "Challan not generated. Please check challan template/data on server." };
            }
        }

        private static string BuildSubmitMessage(string saveMessage, ChallanMailResult challanResult)
        {
            if (challanResult == null)
            {
                return saveMessage;
            }

            if (challanResult.Success)
            {
                return saveMessage + " and Challan Mail Send";
            }

            if (!string.IsNullOrWhiteSpace(challanResult.RelativePath) || !string.IsNullOrWhiteSpace(challanResult.FileName))
            {
                return saveMessage + " and Challan generated, but mail not sent. Please check SMTP relay/access on server.";
            }

            return saveMessage + " but " + challanResult.Message;
        }
        private static PalletDispatcch.DispatchMasterInput CreateDispatchMasterInput(PalletDispatcch.PalletDispatchSaveRequest request)
        {
            return new PalletDispatcch.DispatchMasterInput
            {
                FromLocation = request.FromLocation,
                ShipToLocation = request.ShipToLocation,
                Consignee = request.Consignee,
                TruckNumber = request.TruckNumber,
                DriverName = request.DriverName,
                DriverContactNo = request.DriverContactNo,
                TransporterName = request.TransporterName,
                GenerateChallan = request.GenerateChallan
            };
        }

        private static List<string> ParseClientBatchList(string clientBatchNos)
        {
            var batches = new List<string>();
            if (string.IsNullOrWhiteSpace(clientBatchNos))
            {
                return batches;
            }

            try
            {
                var token = JToken.Parse(clientBatchNos);
                foreach (JToken item in token.Type == JTokenType.Array ? token : new JArray(token))
                {
                    string value = Convert.ToString(item).Trim();
                    if (!string.IsNullOrWhiteSpace(value) && !batches.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        batches.Add(value);
                    }
                }
            }
            catch (Exception)
            {
                foreach (string item in Regex.Split(clientBatchNos, @"[\r\n,]+"))
                {
                    string value = item.Trim();
                    if (!string.IsNullOrWhiteSpace(value) && !batches.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        batches.Add(value);
                    }
                }
            }

            return batches;
        }
    }
}



