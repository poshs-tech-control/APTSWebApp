using System;
using System.Data;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class ChallanController : Controller
    {
        private readonly DataAccess dataAccess = new DataAccess();

        [HttpPost]
        public JsonResult GenerateAndSend(string module, string code)
        {
            if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(code))
            {
                return Json(new { success = false, message = "Module and code are required." });
            }

            try
            {
                ChallanMailResult result;
                string cleanModule = module.Trim().ToLowerInvariant();

                if (cleanModule == "memberdispatch" || cleanModule == "internaltransfer")
                {
                    result = GenerateMemberDispatch(code.Trim());
                }
                else if (cleanModule == "palletdispatch")
                {
                    result = GeneratePalletDispatch(code.Trim());
                }
                else if (cleanModule == "membercollection")
                {
                    result = GenerateMemberCollection(code.Trim());
                }
                else if (cleanModule == "palletdismentle" || cleanModule == "palletdismantle") // Customer
                {
                    result = GeneratePalletDismentle(code.Trim());
                }
                else if (cleanModule == "palletorder")
                {
                    result = GeneratePalletOrder(code.Trim());
                }
                else
                {
                    return Json(new { success = false, message = "Invalid challan module." });
                }

                return Json(new
                {
                    success = result.Success,
                    message = result.Message,
                    fileName = result.FileName,
                    path = result.RelativePath
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return Json(new { success = false, message = "Unable to generate challan mail: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult GenerateMemberDispatchChallan(string dispatchCode)
        {
            return RunSpecific(() => GenerateMemberDispatch(dispatchCode));
        }

        [HttpPost]
        public JsonResult GeneratePalletDispatchChallan(string palletDispatchCode) // Pallet Dispatch Challan
        {
            return RunSpecific(() => GeneratePalletDispatch(palletDispatchCode));
        }

        [HttpPost]
        public JsonResult GenerateMemberCollectionChallan(string collectionCode)
        {
            return RunSpecific(() => GenerateMemberCollection(collectionCode));
        }

        [HttpPost]
        public JsonResult GeneratePalletDismentleChallan(string custDismentleCode)
        {
            return RunSpecific(() => GeneratePalletDismentle(custDismentleCode));
        }

        [HttpPost]
        public JsonResult GeneratePalletOrderChallan(string palletOrderCode)
        {
            return RunSpecific(() => GeneratePalletOrder(palletOrderCode));
        }

        private JsonResult RunSpecific(Func<ChallanMailResult> action)
        {
            try
            {
                ChallanMailResult result = action();
                return Json(new { success = result.Success, message = result.Message, fileName = result.FileName, path = result.RelativePath });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return Json(new { success = false, message = "Unable to generate challan mail: " + ex.Message });
            }
        }

        private ChallanMailResult GenerateMemberDispatch(string dispatchCode)
        {
            DataSet master = dataAccess.GetDispatchDetails(dispatchCode);
            EnsureHasRows(master, "Dispatch details not found.");
            DataRow row = master.Tables[0].Rows[0];

            if (!IsGenerateChallan(row))
            {
                return new ChallanMailResult { Success = false, Message = "Generate Challan is not enabled." };
            }

            DataSet details = master;
            var bll = new ChallenBll
            {
                CustomerName = Value(row, "CustName"),
                DelAddress = FirstValue(row, "ToAddress", "ToLocation"),
                FromLocationType = FirstValue(row, "FromLocationType", "FromLocation"),
                FromAddress = FirstValue(row, "FromAddress", "FromLocation"),
                ToLocationType = FirstValue(row, "ToLocationType", "ToLocation"),
                ToLocation = FirstValue(row, "ToLocation", "ToAddress"),
                ToAddress = FirstValue(row, "ToAddress", "ToLocation"),
                ChallenNo = dispatchCode.Replace("DSP", "DCHL"),
                Deldate = DateTime.Today.ToString("dd-MMM-yyyy"),
                LONO = Value(row, "LeaseOrderNo"),
                TruckNo = FirstValue(row, "TruckNumber", "TruckNo"),
                TruckNumber = FirstValue(row, "TruckNumber", "TruckNo"),
                DriverName = Value(row, "DriverName"),
                Dispatcher = Value(row, "UserName"),
                TransporterName = FirstValue(row, "TransporterName", "transportorname"),
                EmailId = FirstValue(row, "ToEmailId", "Email"),
                UserEmailId = Value(row, "UserMailId")
            };

            return Service.GenerateMemberDispatchAndSendMail(bll, details);
        }

        private ChallanMailResult GeneratePalletDispatch(string palletDispatchCode) 
        {
            DataSet ds = dataAccess.GetPalletDispatchDetailsForChallan(palletDispatchCode);
            EnsureHasRows(ds, "Pallet Dispatch details not found.");
            DataRow row = ds.Tables[0].Rows[0];

            if (!IsGenerateChallan(row))
            {
                return new ChallanMailResult { Success = false, Message = "Generate Challan is not enabled." };
            }

            string fromLocation = FirstValue(row, "FromLocationTitle", "FromLocationName", "LocationTitle", "FromLocation", "DispatchFromLocation");
            string fromAddress = FirstValue(row, "FromAddress", "FromLocationAddress", "DispatchFromAddress", "LocationAddress", "FromLocation");
            string toLocation = FirstValue(row, "ShipToLocTitle", "ShipToLocationTitle", "ToLocation", "ShipToLocation", "CustName", "CustomerName");
            string toAddress = FirstValue(row, "ToAddress", "ShipToLocAddress", "ShipToLocationAddress", "ShipToAddress", "Address", "LocationAddress", "ShipToLocation");

            var bll = new ChallenBll
            {
                CustomerName = FirstValue(row, "CustName", "CustomerName", "ShipToLocTitle", "ShipToLocationTitle"),
                DelAddress = toAddress,
                FromLocation = fromLocation,
                FromLocationType = fromLocation,
                FromAddress = fromAddress,
                ToLocation = toLocation,
                ToLocationType = toLocation,
                ToAddress = toAddress,
                ChallenNo = palletDispatchCode.Replace("PLDP", "PDCHL"),
                Deldate = DateTime.Today.ToString("dd-MMM-yyyy"),
                TruckNo = FirstValue(row, "TruckNo", "TruckNumber", "VehicleNo"),
                TruckNumber = FirstValue(row, "TruckNo", "TruckNumber", "VehicleNo"),
                DriverName = Value(row, "DriverName"),
                Dispatcher = Value(row, "UserName"),
                TransporterName = FirstValue(row, "TransporterName", "Transporter", "TransportCode"),
                EmailId = FirstValue(row, "Email", "EmailId", "ToEmailId"),
                UserEmailId = Value(row, "UserMailId")
            };

            return Service.GeneratePalletDispatchAndSendMail(bll, ds);
        }

        private ChallanMailResult GenerateMemberCollection(string collectionCode)
        {
            DataSet ds = dataAccess.GetMemberCollectionDetails(collectionCode);
            EnsureHasRows(ds, "Member Collection details not found.");
            DataRow row = ds.Tables[0].Rows[0];

            if (!IsGenerateChallan(row))
            {
                return new ChallanMailResult { Success = false, Message = "Generate Challan is not enabled." };
            }

            var bll = new ChallenBll
            {
                CustomerName = FirstValue(row, "CustName", "CustsomerInFromLoc"),
                DelAddress = FirstValue(row, "ToLocation", "VehicleInToLoc", "TruckNo"),
                FromLocationType = FirstValue(row, "FromLocationType", "CustsomerInFromLoc", "FromLocation"),
                FromAddress = Value(row, "FromLocation"),
                CustsomerInFromLoc = Value(row, "CustsomerInFromLoc"),
                VehicleInFromLoc = Value(row, "VehicleInFromLoc"),
                ToLocationType = FirstValue(row, "ToLocationType", "ToLocation", "VehicleInToLoc"),
                ToLocation = FirstValue(row, "ToLocation", "VehicleInToLoc", "TruckNo"),
                ToAddress = FirstValue(row, "ToLocation", "VehicleInToLoc", "TruckNo"),
                CustsomerInToLoc = FirstValue(row, "CustsomerInToLoc", "ToLocation", "VehicleInToLoc", "TruckNo"),
                TransporterName = Value(row, "Transporter"),
                ChallenNo = collectionCode.Replace("CL0", "COCHL"),
                Deldate = DateTime.Today.ToString("dd-MMM-yyyy"),
                TruckNo = Value(row, "TruckNo"),
                DriverName = Value(row, "DriverName"),
                Dispatcher = Value(row, "UserName"),
                EmailId = FirstValue(row, "Email", "CustsomerInFromLocMailId"),
                UserEmailId = FirstValue(row, "UserMailId", "UserName"),
                TotalQty = TableValue(ds, 4, "TotalQty", "Null")
            };

            return Service.GenerateMemberCollectionAndSendMail(bll, ds);
        }

        private ChallanMailResult GeneratePalletDismentle(string custDismentleCode)
        {
            DataSet ds = dataAccess.GetPalletDismentleDetails(custDismentleCode);
            EnsureHasRows(ds, "Pallet Dismentle details not found.");
            DataRow row = ds.Tables[0].Rows[0];

            var bll = new ChallenBll
            {
                CustomerName = Value(row, "CustName"),
                DelAddress = Value(row, "Address"),
                ChallenNo = Value(row, "CustDismentleCode"),
                Deldate = DateTime.Today.ToString("dd-MMM-yyyy"),
                EmailId = Value(row, "Email"),
                UserEmailId = Value(row, "UserName"),
                TotalQty = TableValue(ds, 3, "Total_Qty", "Null"),
                TotalDiscQty = TableValue(ds, 4, "TotalDiscQty", "Null")
            };

            return Service.GenerateDismentleAndSendMail(bll, ds);
        }

        private ChallanMailResult GeneratePalletOrder(string palletOrderCode)
        {
            DataSet ds = dataAccess.GetPalletOrderChallanDetails(palletOrderCode);
            EnsureHasRows(ds, "Pallet Order details not found.");

            var bll = new ChallenBll
            {
                PalletOrderCode = Value(ds.Tables[0].Rows[0], "PalletOrderCode"),
                OrderDate = DateTime.Today.ToString("dd-MMM-yyyy"),
                UserEmailId = TableValue(ds, 1, "UserMailId", string.Empty)
            };

            return Service.GeneratePalletOrderAndSendMail(bll, ds);
        }

        private ChallanMailService Service
        {
            get { return new ChallanMailService(Server); }
        }

        private static void EnsureHasRows(DataSet dataSet, string message)
        {
            if (dataSet == null || dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count == 0)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static bool IsGenerateChallan(DataRow row)
        {
            if (row.Table.Columns.Contains("GenerateChallan"))
            {
                string value = Value(row, "GenerateChallan").Trim();
                return string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static string TableValue(DataSet dataSet, int tableIndex, string columnName, string fallback)
        {
            if (dataSet == null || dataSet.Tables.Count <= tableIndex || dataSet.Tables[tableIndex].Rows.Count == 0)
            {
                return fallback;
            }

            string value = Value(dataSet.Tables[tableIndex].Rows[0], columnName);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string FirstValue(DataRow row, params string[] columnNames)
        {
            foreach (string columnName in columnNames)
            {
                string value = Value(row, columnName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
        private static string Value(DataRow row, string columnName)
        {
            return row != null && row.Table.Columns.Contains(columnName)
                ? Convert.ToString(row[columnName])
                : string.Empty;
        }
    }
}


