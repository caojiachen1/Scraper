using System.Collections.ObjectModel;

namespace Scraper
{
    public class HtmlNode
    {
        public string DisplayText { get; set; }
        public ObservableCollection<HtmlNode> Children { get; set; }
    }
}