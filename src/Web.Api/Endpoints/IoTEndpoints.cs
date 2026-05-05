
namespace Web.Api.Endpoints;

public static class IoTEndpoints
{
    private static List<TowerData> towerStore = new();

    public static void MapIoTEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/iot-data", ReceiveIoTData);
    }

    private static IResult ReceiveIoTData([FromBody] TowerData data)
    {
        // ✅ store data
        towerStore.Add(data);

        return Results.Ok(data);
    }
}

public class TowerData
{
    public string TowerId { get; set; }
    public double Temperature { get; set; }
    public double BatteryLevel { get; set; }
    public string Location { get; set; }
}