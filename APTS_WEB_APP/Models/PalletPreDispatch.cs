namespace APTS_WEB_APP.Models
{
    public class PalletPreDispatch
    {
        public class PreDispatchValidationResult
        {
            public bool IsAllowed { get; set; }

            public string Message { get; set; }

            public string PalletCode { get; set; }

            public string PalletCreatedDate { get; set; }

            public string RfidNo { get; set; }

            public string PalletStatus { get; set; }
        }

        public class PalletOrderDimensions
        {
            public string Width { get; set; }

            public string Length1 { get; set; }

            public string Length2 { get; set; }
        }
        public class ExistingPreDispatchItem
        {
            public string PalletPreDispatchCode { get; set; }

            public string PalletCode { get; set; }

            public string ClientBatchNo { get; set; }

            public string SheetLength { get; set; }

            public string Width { get; set; }

            public string Weight { get; set; }

            public string Thikness { get; set; }

            public string ClientLocation { get; set; }
        }
    }
}
