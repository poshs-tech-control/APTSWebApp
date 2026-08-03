using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class ReportController : Controller
    {
        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult Report()
        {
            return View();
        }

        public ActionResult CurrentStockReport()
        {
            string userCode = dataAccess.GetCurrentUserCode(this);
            var model = new CurrentStockReport
            {
                ClientList = dataAccess.LoadCurrentStockClients(userCode)
            };

            if (model.ClientList.Count > 0)
            {
                model.ClientCode = model.ClientList[0].Value;
            }

            return View(model);
        }

        [HttpGet]
        public JsonResult GetCurrentStockClientOptions(string stockLocation)
        {
            string userCode = dataAccess.GetCurrentUserCode(this);
            string locationType = string.IsNullOrWhiteSpace(stockLocation) ? "Client" : stockLocation.Trim();

            if (string.Equals(locationType, "Client", System.StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    success = true,
                    options = dataAccess.LoadCurrentStockClients(userCode),
                    valueField = "CustomerCode",
                    textField = "CustName"
                }, JsonRequestBehavior.AllowGet);
            }

            if (string.Equals(locationType, "Factory", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(locationType, "Warehouse", System.StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    success = true,
                    options = dataAccess.LoadCurrentStockLocationsByType(GetCurrentStockLocationTypeCode(locationType), userCode),
                    valueField = "LocationCode",
                    textField = "LocationTitle"
                }, JsonRequestBehavior.AllowGet);
            }

            if (string.Equals(locationType, "Truck", System.StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    success = true,
                    options = dataAccess.LoadCurrentStockTrucks(),
                    valueField = "TruckNo",
                    textField = "TruckNo"
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                options = new CurrentStockReport.DropdownItem[0]
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GetCurrentStockReport(CurrentStockReport.ReportRequest request)
        {
            request = request ?? new CurrentStockReport.ReportRequest();

            if (string.IsNullOrWhiteSpace(request.LocationCode))
            {
                return Json(new { success = false, message = "Please select location.", rows = new CurrentStockReport.ReportRow[0] });
            }

            if (request.ReportDate == default(System.DateTime))
            {
                return Json(new { success = false, message = "Please select date.", rows = new CurrentStockReport.ReportRow[0] });
            }

            return Json(new
            {
                success = true,
                rows = dataAccess.LoadCurrentStockReport(request.LocationCode, request.ReportDate, request.ReportDate)
            });
        }

        private static string GetCurrentStockLocationTypeCode(string stockLocation)
        {
            if (string.Equals(stockLocation, "Factory", System.StringComparison.OrdinalIgnoreCase))
            {
                return "MISC0000019";
            }

            if (string.Equals(stockLocation, "Warehouse", System.StringComparison.OrdinalIgnoreCase))
            {
                return "MISC0000020";
            }

            return stockLocation;
        }

        public ActionResult QueryReport()
        {
            return View();
        }

        [HttpPost]
        public JsonResult GetQueryReport(QueryReport.Request request)
        {
            request = request ?? new QueryReport.Request();

            request.Rfid = QrOcrHelper.NormalizeQrScanValue(request.Rfid);

            if (string.IsNullOrWhiteSpace(request.Rfid))
            {
                return Json(new { success = false, message = "Please scan or enter member RFID.", item = (QueryReport.Result)null });
            }

            QueryReport.Result item = dataAccess.LoadQueryReport(request.Rfid);

            if (item == null || string.IsNullOrWhiteSpace(item.RFID))
            {
                return Json(new { success = false, message = "No data found for scanned member.", item = (QueryReport.Result)null });
            }

            return Json(new { success = true, item = item });
        }

        public ActionResult DiscrepancyReport()
        {
            return View();
        }

        [HttpPost]
        public JsonResult GetDiscrepancyReport(DiscrepancyReport.Request request)
        {
            request = request ?? new DiscrepancyReport.Request();

            if (string.IsNullOrWhiteSpace(request.DiscrepancyType))
            {
                return Json(new { success = false, message = "Please select discrepancy type.", result = new DiscrepancyReport.Result() });
            }

            if (request.FromDate == default(System.DateTime) || request.ToDate == default(System.DateTime))
            {
                return Json(new { success = false, message = "Please select from date and to date.", result = new DiscrepancyReport.Result() });
            }

            if (request.FromDate.Date > request.ToDate.Date)
            {
                return Json(new { success = false, message = "From date cannot be greater than to date.", result = new DiscrepancyReport.Result() });
            }

            DiscrepancyReport.Result result = dataAccess.LoadDiscrepancyReport(request.DiscrepancyType, request.FromDate, request.ToDate);

            return Json(new
            {
                success = true,
                result = result
            });
        }

        public ActionResult HighAgeReport()
        {
            string userCode = dataAccess.GetCurrentUserCode(this);
            var model = new HighAgeReport
            {
                LocationList = dataAccess.LoadHighAgeClientLocations(userCode)
            };

            if (model.LocationList.Count > 0)
            {
                model.LocationCode = model.LocationList[0].Value;
            }

            return View(model);
        }

        [HttpGet]
        public JsonResult GetHighAgeLocationOptions(string highAgeType)
        {
            string userCode = dataAccess.GetCurrentUserCode(this);
            string reportType = string.IsNullOrWhiteSpace(highAgeType) ? "Client" : highAgeType.Trim();

            if (string.Equals(reportType, "Customer", System.StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    success = true,
                    options = dataAccess.LoadHighAgeCustomerLocations(userCode),
                    valueField = "ShipToLocCode",
                    textField = "ShipToLocTitle"
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                options = dataAccess.LoadHighAgeClientLocations(userCode),
                valueField = "LocationCode",
                textField = "CustName"
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GetHighAgeReport(HighAgeReport.ReportRequest request)
        {
            request = request ?? new HighAgeReport.ReportRequest();

            if (string.IsNullOrWhiteSpace(request.HighAgeType))
            {
                return Json(new { success = false, message = "Please select report location type.", result = new HighAgeReport.ReportResult() });
            }

            if (string.IsNullOrWhiteSpace(request.LocationCode))
            {
                return Json(new { success = false, message = "Please select location.", result = new HighAgeReport.ReportResult() });
            }

            int totalDays;
            if (!int.TryParse(request.TotalDays, out totalDays) || totalDays < 0)
            {
                return Json(new { success = false, message = "Please enter valid total days.", result = new HighAgeReport.ReportResult() });
            }

            HighAgeReport.ReportResult result = dataAccess.LoadHighAgeReport(request.HighAgeType, request.LocationCode, request.TotalDays);

            return Json(new
            {
                success = true,
                result = result
            });
        }

        public ActionResult MemberTransactionReport()
        {
            string userCode = dataAccess.GetCurrentUserCode(this);
            var model = new CurrentStockReport
            {
                ClientList = dataAccess.LoadCurrentStockClients(userCode)
            };

            if (model.ClientList.Count > 0)
            {
                model.ClientCode = model.ClientList[0].Value;
            }

            return View(model);
        }

        [HttpPost]
        public JsonResult GetMemberTransactionReport(MemberTransactionReport.ReportRequest request)
        {
            request = request ?? new MemberTransactionReport.ReportRequest();

            if (string.IsNullOrWhiteSpace(request.LocationCode))
            {
                return Json(new { success = false, message = "Please select location.", rows = new MemberTransactionReport.ReportRow[0] });
            }

            if (request.FromDate == default(System.DateTime) || request.ToDate == default(System.DateTime))
            {
                return Json(new { success = false, message = "Please select from date and to date.", rows = new MemberTransactionReport.ReportRow[0] });
            }

            if (request.FromDate.Date > request.ToDate.Date)
            {
                return Json(new { success = false, message = "From date cannot be greater than to date.", rows = new MemberTransactionReport.ReportRow[0] });
            }

            return Json(new
            {
                success = true,
                rows = dataAccess.LoadMemberTransactionReport(request.LocationCode, request.FromDate, request.ToDate)
            });
        }
    }
}
