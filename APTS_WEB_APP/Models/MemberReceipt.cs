using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class MemberReceipt
    {
        public class LocationItem
        {
            public string LocationCode { get; set; }

            public string CustomerCode { get; set; }

            public string LocationTitle { get; set; }

            public string LocationCity { get; set; }

            public string LocationType { get; set; }

            public string LocationTypeName { get; set; }
        }

        public class CustomerItem
        {
            public string CustomerCode { get; set; }

            public string CustName { get; set; }

            public string CustType { get; set; }

            public string LocationCode { get; set; }
        }

        public class ReceiptSetupInput
        {
            public string ReceiptFrom { get; set; }

            public string ReceiptLocation { get; set; }

            public string ReceiptLocationName { get; set; }

            public List<LocationItem> FactoryLocations { get; set; }

            public List<LocationItem> WarehouseLocations { get; set; }

            public List<CustomerItem> ClientLocations { get; set; }
        }

        public class ReceiveMemberItem
        {
            public string Rfid { get; set; }

            public string MemberCode { get; set; }

            public string MemberType { get; set; }

            public string MemberSize { get; set; }

            public string MatDispCode { get; set; }

            public string TruckNo { get; set; }
        
            public string LastTransferTo { get; set; }
        }

        public class ReceiveValidationResult
        {
            public bool Success { get; set; }

            public string Rfid { get; set; }

            public string Message { get; set; }

            public List<ReceiveMemberItem> Members { get; set; }
        }

        public class ReceiveMemberInput
        {
            public string Rfid { get; set; }

            public string MemberCode { get; set; }

            public string MemberType { get; set; }

            public string MemberSize { get; set; }

            public string MatDispCode { get; set; }

            public string TruckNo { get; set; }

            public string ActionType { get; set; }

            public string Comment { get; set; }
        }

        public class ReceiveSaveRequest
        {
            public string ReceiptFrom { get; set; }

            public string ReceiptLocation { get; set; }

            public string ReceiptLocationName { get; set; }

            public List<ReceiveMemberInput> Members { get; set; }
        }

        public class ReceiveSaveResult
        {
            public bool Success { get; set; }

            public string MaterialRecCode { get; set; }

            public int DetailCount { get; set; }

            public string Message { get; set; }
        }
    }
}
