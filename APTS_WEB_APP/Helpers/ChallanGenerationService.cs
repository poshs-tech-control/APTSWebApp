using System;
using System.Data;
using System.Web;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Helpers
{
    public class ChallanGenerationService
    {
        private readonly DataAccess dataAccess;
        private readonly ChallanMailService mailService;

        public ChallanGenerationService(DataAccess dataAccess, HttpServerUtilityBase server)
        {
            this.dataAccess = dataAccess;
            this.mailService = new ChallanMailService(server);
        }

        public ChallanMailResult GenerateMemberDispatch(string dispatchCode)
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
                UserEmailId = Value(row, "UserMailId"),
                TotalQty = InternalTransferTotalQty(details, row)
            };

            return mailService.GenerateMemberDispatchAndSendMail(bll, details);
        }

        public ChallanMailResult GeneratePalletDispatch(string palletDispatchCode)
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

            return mailService.GeneratePalletDispatchAndSendMail(bll, ds);
        }

        public ChallanMailResult GenerateMemberCollection(string collectionCode)
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

            return mailService.GenerateMemberCollectionAndSendMail(bll, ds);
        }

        public ChallanMailResult GeneratePalletDismentle(string custDismentleCode)
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

            return mailService.GenerateDismentleAndSendMail(bll, ds);
        }

        public ChallanMailResult GeneratePalletOrder(string palletOrderCode)
        {
            DataSet ds = dataAccess.GetPalletOrderChallanDetails(palletOrderCode);
            EnsureHasRows(ds, "Pallet Order details not found.");

            var bll = new ChallenBll
            {
                PalletOrderCode = Value(ds.Tables[0].Rows[0], "PalletOrderCode"),
                OrderDate = DateTime.Today.ToString("dd-MMM-yyyy"),
                UserEmailId = TableValue(ds, 1, "UserMailId", string.Empty)
            };

            return mailService.GeneratePalletOrderAndSendMail(bll, ds);
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

        private static string InternalTransferTotalQty(DataSet dataSet, DataRow masterRow)
        {
            if (dataSet != null && dataSet.Tables.Count > 1)
            {
                DataTable detailTable = dataSet.Tables[1];
                if (detailTable.Columns.Contains("RFID"))
                {
                    return detailTable.Rows.Count.ToString();
                }

                if (detailTable.Columns.Contains("TOTAL"))
                {
                    int total = 0;
                    foreach (DataRow detailRow in detailTable.Rows)
                    {
                        int qty;
                        if (int.TryParse(Convert.ToString(detailRow["TOTAL"]), out qty))
                        {
                            total += qty;
                        }
                    }

                    return total.ToString();
                }
            }

            return FirstValue(masterRow, "TotalDispatch");
        }

        private static string Value(DataRow row, string columnName)
        {
            return row != null && row.Table.Columns.Contains(columnName)
                ? Convert.ToString(row[columnName])
                : string.Empty;
        }
    }
}


