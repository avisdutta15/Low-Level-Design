using Newtonsoft.Json;
using System.Xml;

namespace XMLToJson
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
            if (data.Trim().StartsWith("{") == false)
            {
                throw new Exception("Data is in JSON format. Cannot render!");
            }
            Console.WriteLine($"Rendering data: {data}");
        }
    }

    // 3. Adapter
    //  The Interface (What the Client expects)
    // "I need a provider that gives me JSON string"
    public interface IJsonDataAdapter
    {
        string GetJsonData();
    }

    public class XmlToJsonAdapter : IJsonDataAdapter
    {
        private readonly StockFeed _stockFeed;
        public XmlToJsonAdapter(StockFeed stockFeed)
        {
            _stockFeed = stockFeed;
        }
        public string GetJsonData() 
        {
            // Step A: Get the data in the original format (XML)
            string xmlDocument = _stockFeed.GetStockData();

            // Step B: Convert it (The "Adaptation" logic)
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlDocument);

            // Convert XML Node to JSON String
            string json = JsonConvert.SerializeXmlNode(doc);

            // Step C: Return the format the client expects
            return json;
        }
    }

    public class DashboardApp
    {
        public void ShowCharts()
        {
            StockFeed feed = new StockFeed();

            // Wrap the legacy system in the adapter
            IJsonDataAdapter adapter = new XmlToJsonAdapter(feed);

            // Usage
            // The App thinks it's talking to a modern JSON provider.
            // It has NO IDEA that XML parsing is happening behind the scenes.
            string data = adapter.GetJsonData();

            Console.WriteLine($"Chart Rendered with: {data}");
        }
    }

    // 4. The Application
    public class Program
    {
        public static void Main(string[] args)
        {
            DashboardApp app = new DashboardApp();
            app.ShowCharts();
        }
    }
}

/*
    The Adapter pattern involves a single class called the Adapter, which is responsible for 
    joining functionalities of independent or incompatible interfaces.

    Let’s go through the components of the Adapter pattern:
    1. Target Interface: This is the interface that the client code expects to interact with. 
                         It’s the interface that your client code uses.
    2. Adaptee: This is the class that has the functionality that the client code wants to use, 
                but it doesn’t conform to the target interface.
    3. Adapter: This is the class that bridges the gap between the Target Interface and the Adaptee. 
                It implements the Target Interface and delegates the calls to the Adaptee.

    In this case: 
    Target Interface : FancyCharts (JSON service)
    Adaptee          : StockFeed (XML service)
    Adapter          : XMLToJson
 */