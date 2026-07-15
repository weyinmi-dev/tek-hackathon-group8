namespace Modules.Network.UnitTests.Ingestion.Parsers;

/// <summary>
/// The reference Site Snapshot payload, verbatim as MTN's OSS feed emits it. Kept byte-for-byte
/// rather than hand-trimmed so the parser is tested against the real document — including the
/// fields we deliberately ignore.
/// </summary>
internal static class SiteSnapshotFixture
{
    /// <summary>
    /// One complete snapshot: a Lagos site running on generator with the grid down, a critical
    /// power alarm and an acknowledged cooling alarm open against it.
    /// </summary>
    public const string MtnLagos = """
    {
      "requestId": "6a0f8b94-4a1d-43a4-a9c8-f0e87691d551",
      "provider": "MTN Nigeria",
      "environment": "Production",
      "generatedAt": "2026-07-14T12:15:30Z",

      "site": {
        "siteId": "MTN-LAG-0456",
        "siteCode": "LAG0456",
        "siteName": "Lekki Phase 1 Tower",
        "region": "Lagos",
        "cluster": "Lagos East",
        "latitude": 6.447325,
        "longitude": 3.472181,
        "technology": ["2G", "3G", "4G", "5G"],
        "vendor": "Huawei",
        "status": "Operational",
        "healthScore": 87,
        "commissionedDate": "2021-08-16",
        "lastHeartbeat": "2026-07-14T12:15:01Z",

        "equipment": [
          { "equipmentId": "BBU-001", "type": "Baseband Unit", "model": "BBU5900", "status": "Healthy" },
          { "equipmentId": "RRU-001", "type": "Remote Radio Unit", "model": "RRU5302", "status": "Healthy" },
          { "equipmentId": "GEN-001", "type": "Generator", "model": "Perkins 150KVA", "status": "Running" },
          { "equipmentId": "BAT-001", "type": "Battery Bank", "status": "Charging" }
        ]
      },

      "environmentalMetrics": {
        "temperature": 37.6,
        "humidity": 64,
        "batteryVoltage": 48.2,
        "generatorFuelPercent": 41,
        "generatorRunning": true,
        "mainPowerAvailable": false,
        "airConditionerStatus": "Running",
        "doorOpen": false,
        "smokeDetected": false
      },

      "performanceMetrics": {
        "measurementInterval": "15 Minutes",
        "capturedAt": "2026-07-14T12:15:00Z",
        "availabilityPercent": 99.91,
        "connectedUsers": 682,
        "activeVoiceCalls": 74,
        "activeDataSessions": 608,
        "downlinkTrafficGb": 148.3,
        "uplinkTrafficGb": 27.4,
        "averageDownlinkMbps": 752,
        "averageUplinkMbps": 108,
        "packetLossPercent": 0.28,
        "latencyMs": 18,
        "callSetupSuccessRate": 99.72,
        "callDropRate": 0.33,
        "handoverSuccessRate": 98.94,
        "cellUtilizationPercent": 76,
        "kpis": [
          { "name": "PRB Utilization", "value": 71.2, "unit": "%" },
          { "name": "SINR", "value": 25.7, "unit": "dB" },
          { "name": "RSRP", "value": -91, "unit": "dBm" }
        ]
      },

      "activeAlarms": [
        {
          "alarmId": "ALM-100284",
          "severity": "Critical",
          "category": "Power",
          "type": "Grid Power Failure",
          "status": "Active",
          "source": "Generator Controller",
          "raisedAt": "2026-07-14T11:47:00Z",
          "description": "Commercial power unavailable. Generator currently supplying power."
        },
        {
          "alarmId": "ALM-100291",
          "severity": "Major",
          "category": "Cooling",
          "type": "High Temperature Warning",
          "status": "Acknowledged",
          "raisedAt": "2026-07-14T12:01:00Z",
          "description": "Equipment shelter temperature exceeded threshold."
        }
      ],

      "maintenance": {
        "lastMaintenanceDate": "2026-06-22",
        "nextScheduledMaintenance": "2026-08-20",

        "openTickets": [
          {
            "ticketId": "TT-20491",
            "priority": "High",
            "status": "Assigned",
            "assignedEngineer": { "engineerId": "ENG-091", "name": "Adewale Johnson" },
            "issue": "Power instability investigation",
            "createdAt": "2026-07-14T11:50:00Z",
            "estimatedArrival": "2026-07-14T13:30:00Z"
          }
        ],

        "maintenanceHistory": [
          {
            "ticketId": "TT-20102",
            "completedAt": "2026-06-22T16:10:00Z",
            "engineer": "Grace Okafor",
            "action": "Replaced battery bank"
          },
          {
            "ticketId": "TT-19873",
            "completedAt": "2026-05-14T10:41:00Z",
            "engineer": "Samuel Bello",
            "action": "Serviced generator"
          }
        ]
      }
    }
    """;
}
