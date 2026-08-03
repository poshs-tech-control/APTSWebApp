using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class InternalTransfer
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

        public class TransportItem
        {
            public string TransportCode { get; set; }

            public string VehicleNo { get; set; }

            public string TransporterName { get; set; }

            public string DriverName { get; set; }

            public string MobileNo { get; set; }

            public string Active { get; set; }
        }

        public class MemberValidationResult
        {
            public bool IsValid { get; set; }

            public string Rfid { get; set; }

            public string MemberType { get; set; }

            public string MemberSize { get; set; }

            public string MemberCode { get; set; }

            public string MemberWeight { get; set; }

            public string MemberDimension { get; set; }

            public string Active { get; set; }

            public string Message { get; set; }
        }

        public class CollectionBulkValidationResult
        {
            public bool IsValid { get; set; }

            public bool BypassRequired { get; set; }

            public string Rfid { get; set; }

            public string Message { get; set; }

            public List<TransferMemberInput> Members { get; set; }
        }

        public class TransferMemberInput
        {
            public string Rfid { get; set; }

            public string TagRfid { get; set; }

            public string MemberCode { get; set; }

            public string MemberType { get; set; }

            public string MemberSize { get; set; }

            public string MemberWeight { get; set; }

            public string MemberDimension { get; set; }
        }

        public class DispatchSetupInput
        {
            public string UserLocationType { get; set; }

            public string UserLocation { get; set; }

            public string ToLocationType { get; set; }

            public string ToLocation { get; set; }

            public List<TransferMemberInput> Members { get; set; }
        }

        public class DispatchSaveRequest : DispatchSetupInput
        {
            public string TruckNumber { get; set; }

            public string DriverName { get; set; }

            public string DriverContactNo { get; set; }

            public string TransporterName { get; set; }

            public bool GenerateChallan { get; set; }
        }

        public class DispatchSaveResult
        {
            public bool Success { get; set; }

            public string MatDispCode { get; set; }

            public int TotalRecords { get; set; }

            public string Message { get; set; }
        }
    }
}

