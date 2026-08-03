namespace APTS_WEB_APP.Models
{
    public class PalletOrder
    {
        public class CustomerListItem
        {
            public string CustomerCode { get; set; }

            public string CustName { get; set; }

            public string CustType { get; set; }

            public string LocationCode { get; set; }
        }

        public class MiscParameterItem
        {
            public string ParameterCode { get; set; }

            public string ParameterName { get; set; }

            public string ParameterType { get; set; }

            public string TransactionID { get; set; }
        }

        public class PalletSkuItem
        {
            public string PalletSkewCode { get; set; }

            public string Shape { get; set; }

            public string SheetWidth { get; set; }

            public string SheetLength1 { get; set; }

            public string SheetLength2 { get; set; }

            public string Stacking { get; set; }

            public string CMSize { get; set; }

            public string LM1Size { get; set; }

            public string LM2Size { get; set; }

            public string CMSizeQty { get; set; }

            public string LM1SizeQty { get; set; }

            public string LM2SizeQty { get; set; }

            public string Remarks { get; set; }
        }

        public class PalletOrderLineItem
        {
            public string SkuCode { get; set; }

            public string Qty { get; set; }
        }

        public class PalletOrderSaveResult
        {
            public string PalletOrderCode { get; set; }

            public int DetailCount { get; set; }
        }
    }
}
