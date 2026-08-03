using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class MemberCollection
    {
        public class CustomerShipToLocationItem
        {
            public string ShipToLocCode { get; set; }
            public string ShipToLocTitle { get; set; }
            public string ShipToLocCompany { get; set; }
            public string LocationAddress { get; set; }
            public string EmailId { get; set; }
        }

        public class TransportItem
        {
            public string TransportCode { get; set; }
            public string VehicleNo { get; set; }
            public string TransporterName { get; set; }
            public string DriverName { get; set; }
            public string MobileNo { get; set; }
            public string Active { get; set; }
        }

        public class CollectionMemberItem
        {
            public string CustDismentleCode { get; set; }
            public string CustCode { get; set; }
            public string RFIDNo { get; set; }
            public string MemberType { get; set; }
            public string MemberSize { get; set; }
            public string ActionType { get; set; }
        }

        public class MemberCollectionSetupInput
        {
            public string Customer { get; set; }
            public string Truck { get; set; }
            public List<CollectionMemberItem> Members { get; set; }
        }

        public class MemberCollectionSaveRequest
        {
            public string FromLocation { get; set; }
            public string TransportCode { get; set; }
            public string TruckNo { get; set; }
            public string DriverName { get; set; }
            public string DriverContactNo { get; set; }
            public string TransporterName { get; set; }
            public List<CollectionMemberItem> Members { get; set; }
        }

        public class MemberCollectionSaveResult
        {
            public bool Success { get; set; }
            public string CollectionCode { get; set; }
            public int TotalRecords { get; set; }
            public string Message { get; set; }
        }
    }
}