namespace Web.Api.Endpoints;

public static class IoTEndpoints
{
    private static List<TowerData> towerStore = new();

    public static void MapIoTEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/iot-data", ReceiveIoTData);
    }

    private static IResult ReceiveIoTData(TowerData data)
    {
        // Store data
        towerStore.Add(data);

        // AI decision
        var decision = AIEngine.Process(data);

        return Results.Ok(new
        {
            Tower = data,
            Decision = decision
        });
    }
}

// Model
public class TowerData
{
    public string TowerId { get; set; }
    public double Temperature { get; set; }
    public double BatteryLevel { get; set; }
    public string Location { get; set; }
}

// AI Engine
public static class AIEngine
{
    public static object Process(TowerData tower)
    {
        if (tower.BatteryLevel < 25)
        {
            return new
            {
                Alert = "Low Power Risk",
                Action = "Switch to backup power"
            };
        }

        if (tower.Temperature > 80)
        {
            return new
            {
                Alert = "Overheating Risk",
                Action = "Activate cooling system"
            };
        }

        return new
        {
            Status = "Normal",
            Action = "No action required"
        };
    }
}


