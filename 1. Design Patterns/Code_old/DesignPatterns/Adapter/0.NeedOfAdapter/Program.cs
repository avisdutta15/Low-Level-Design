// Legacy Systems
namespace NeedOfAdapter
{
    // 1. The Legacy System (Returns XML)
    public class StockFeed
    {
        public string GetStockData()
        {
            return "<stocks><stock><symbol>AAPL</symbol><price>150</price></stock></stocks>";
        }
    }

    // 2. The Modern Chart Library (Expects JSON)
    public class FancyCharts
    {
        public void RenderChart(string data)
        {
            if(data.Trim().StartsWith("{") == false)
            {
                throw new Exception("Data is in JSON format. Cannot render!");
            }
            Console.WriteLine($"Rendering data: {data}");
        }
    }

    // 3. The Client (Fails)
    public class DashboardApp
    {
        public void ShowCharts()
        {
            StockFeed feed = new StockFeed();
            FancyCharts chart = new FancyCharts();

            // CRITICAL ERROR: Incompatible Formats
            //chart.RenderChart(feed.GetStockData()); // THROWS EXCEPTION
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            DashboardApp app = new DashboardApp();
            app.ShowCharts();
        } 
    }
}