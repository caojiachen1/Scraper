using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Scraper
{
    public class UrlHistoryItem
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("scrapingResult")]
        public List<HtmlNode> ScrapingResult { get; set; }

        public UrlHistoryItem(string url, List<HtmlNode> scrapingResult = null)
        {
            Url = url;
            ScrapingResult = scrapingResult ?? new List<HtmlNode>();
        }
    }
}