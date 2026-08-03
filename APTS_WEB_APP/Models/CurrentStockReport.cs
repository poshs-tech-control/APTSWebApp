using System;
using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class CurrentStockReport
    {
        public string StockLocation { get; set; }

        public string ClientCode { get; set; }

        public DateTime ReportDate { get; set; }

        public List<DropdownItem> ClientList { get; set; }

        public CurrentStockReport()
        {
            StockLocation = "Client";
            ReportDate = DateTime.Today;
            ClientList = new List<DropdownItem>();
        }

        public class DropdownItem
        {
            public string Value { get; set; }

            public string Text { get; set; }
        }

        public class ReportRequest
        {
            public string LocationCode { get; set; }

            public DateTime ReportDate { get; set; }
        }

        public class ReportRow
        {
            public string ParameterName { get; set; }

            public string MemberSize { get; set; }

            public int Count { get; set; }
        }
    }
}
