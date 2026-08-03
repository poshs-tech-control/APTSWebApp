namespace APTS_WEB_APP.Models
{
    public class SkuListItem
    {
        public string SKEWCode { get; set; }

        public decimal? Weight { get; set; }

        public string MemberSize { get; set; }

        public string Active { get; set; }

        public string TextColumn { get; set; }

        public string MemberTypeText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TextColumn))
                {
                    return string.Empty;
                }

                string[] parts = TextColumn.Split('|');
                return parts.Length > 1 ? parts[1].Trim() : string.Empty;
            }
        }
    }
}
