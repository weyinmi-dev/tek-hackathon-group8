using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace IotSimulator;

class Program
{
    private static readonly HttpClient Http = new();
    private const string ApiUrl = "http://localhost:5000/api/v1/towers/{0}/fuel";

    static async Task Main(string[] args)
    {
        string towerCode = args.Length > 0 ? args[0] : "TWR-JOS-01";
        double currentFuel = 500.0;
        var rnd = new Random();

        Console.WriteLine($"Starting C# IoT Sensor Simulation for {towerCode}...");
        Console.WriteLine("Press 'Ctrl+C' to stop.");

        while (true)
        {
            // Normal burn rate: ~1 liter per interval
            double burn = rnd.NextDouble() + 0.5; // 0.5 to 1.5
            
            currentFuel -= burn;
            if (currentFuel < 0) currentFuel = 0;

            await SendReadingAsync(towerCode, currentFuel);
            await Task.Delay(5000);
        }
    }

    private static async Task SendReadingAsync(string towerCode, double fuelLevel)
    {
        var url = string.Format(ApiUrl, towerCode);
        var payload = new { ActivePowerSource = 1, FuelLevelLiters = fuelLevel };
        
        try
        {
            var response = await Http.PostAsJsonAsync(url, payload);
            Console.WriteLine($"[IoT] Sent reading for {towerCode}: {fuelLevel:F1}L -> API Status: {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IoT] Connection error: {ex.Message}");
        }
    }
}
