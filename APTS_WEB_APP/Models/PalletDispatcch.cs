using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class PalletDispatcch
    {
        public class PalletDispatchDetailsInput
        {
            public string FromLocation { get; set; }

            public string Customer { get; set; }

            public string Consignee { get; set; }

            public List<DispatchItemInput> DispatchItems { get; set; }
        }

        public class PalletDispatchSaveRequest
        {
            public string FromLocation { get; set; }

            public string ShipToLocation { get; set; }

            public string Consignee { get; set; }

            public string TruckNumber { get; set; }

            public string DriverName { get; set; }

            public string DriverContactNo { get; set; }

            public string TransporterName { get; set; }

            public bool GenerateChallan { get; set; }

            public List<DispatchItemInput> DispatchItems { get; set; }
        }

        public class DispatchItemInput
        {
            public string PreDispatchCode { get; set; }

            public string PalletCode { get; set; }

            public string ClientBatchNo { get; set; }

            public string SheetSize { get; set; }
        }

        public class DispatchOcrItem
        {
            public string PreDispatchCode { get; set; }

            public string ClientBatchNo { get; set; }

            public string SheetSize { get; set; }
        }

        public class ShipToLocationItem
        {
            public string ShipToLocCode { get; set; }

            public string ShipToLocTitle { get; set; }

            public string ShipToLocCompany { get; set; }

            public string LocationAddress { get; set; }

            public string EmailId { get; set; }
        }

        public class DispatchMasterInput
        {
            public string FromLocation { get; set; }

            public string ShipToLocation { get; set; }

            public string Consignee { get; set; }

            public string TruckNumber { get; set; }

            public string DriverName { get; set; }

            public string DriverContactNo { get; set; }

            public string TransporterName { get; set; }

            public bool GenerateChallan { get; set; }
        }

        public class DispatchDetailInput
        {
            public string PalletCode { get; set; }

            public string ClientBatchNo { get; set; }
        }

        public class DispatchValidationResult
        {
            public bool IsValid { get; set; }

            public string Message { get; set; }

            public string LastStatus { get; set; }

            public DispatchBatchItem Item { get; set; }
        }

        public class DispatchBatchItem
        {
            public string PreDispatchCode { get; set; }

            public string PalletCode { get; set; }

            public string ClientBatchNo { get; set; }

            public string SheetLength { get; set; }

            public string Width { get; set; }

            public string Weight { get; set; }

            public string Thikness { get; set; }

            public string ClientLocation { get; set; }
        }

        public class BulkDispatchSaveResult
        {
            public bool Success { get; set; }

            public string PalletDispCode { get; set; }

            public int TotalRecords { get; set; }

            public string Message { get; set; }
        }
    }
}
