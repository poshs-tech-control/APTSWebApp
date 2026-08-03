using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class MemberTagging
    {
        public class MemberDetails
        {
            public string MemberCode { get; set; }

            public string Rfid { get; set; }

            public string SkuCode { get; set; }

            public string CurrentLocation { get; set; }
        }

        public class TaggingRequest
        {
            public string MemberCode { get; set; }

            public string Rfid { get; set; }

            public List<string> StickerCodes { get; set; }
        }

        public class SaveResult
        {
            public bool Success { get; set; }

            public string Message { get; set; }

            public int TotalRecords { get; set; }
        }
    }
}
