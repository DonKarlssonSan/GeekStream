using System;

namespace GeekStream.Core.Views
{
    [Serializable]
    public class SearchResultView
    {
        public FeedItemView[] Items { get; set; }
        public string Query { get; set; }
        public int TotalResults { get; set; }
    }
}