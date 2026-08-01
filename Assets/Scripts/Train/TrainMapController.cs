using System.Collections.Generic;
using UnityEngine;

public enum TrainStationKind
{
    Depot,
    PassengerPlatform,
    CargoStation
}

public class TrainTrackNode
{
    public string id;
    public Vector2 position;

    public TrainTrackNode(string id, Vector2 position)
    {
        this.id = id;
        this.position = position;
    }
}

public class TrainTrackSegment
{
    public string id;
    public int fromNode;
    public int toNode;

    public TrainTrackSegment(string id, int fromNode, int toNode)
    {
        this.id = id;
        this.fromNode = fromNode;
        this.toNode = toNode;
    }
}

public class TrainSwitch
{
    public bool upperBranch = true;
}

public class TrainStationRuntime
{
    public string id;
    public string title;
    public TrainStationKind kind;
    public Vector2 position;
    public int lane;
    public int routeIndex;
    public float stopRadius;
    public readonly List<Vector2> carSlots = new List<Vector2>();
    public bool acceptsPassengers;
    public bool handlesCargo;
    public int waitingPassengers;
    public string passengerDestinationId;
    public string cargoId;
    public string cargoTitle;
    public Color cargoColor;
    public Color routeColor;
    public readonly List<TrainPassengerRuntime> waitingPassengerList = new List<TrainPassengerRuntime>();
    public RectTransform rect;
    public TextMeshLabel label;
}

public enum TrainPassengerLocation
{
    Station,
    Car
}

public class TrainPassengerRuntime
{
    public string id;
    public string destinationStationId;
    public string originStationId;
    public string currentStationId;
    public TrainCarRuntime currentCar;
    public TrainPassengerLocation location;
    public Color routeColor;
    public RectTransform dotRect;
    public bool isAnimating;
}

public class TrainMapController : MonoBehaviour
{
    public readonly List<TrainTrackNode> Nodes = new List<TrainTrackNode>();
    public readonly List<TrainTrackSegment> Segments = new List<TrainTrackSegment>();
    public readonly List<TrainStationRuntime> Stations = new List<TrainStationRuntime>();
    public readonly TrainSwitch MainSwitch = new TrainSwitch();

    public const int DepotToSwitchSegment = 0;
    public const int SwitchToPlatformSegment = 1;
    public const int SwitchToCargoSegment = 2;
    public const int PlatformToCitySegment = 3;
    public const int CargoToQuarrySegment = 4;

    public void BuildDefaultMap()
    {
        Nodes.Clear();
        Segments.Clear();
        Stations.Clear();
        MainSwitch.upperBranch = true;

        Nodes.Add(new TrainTrackNode("depot", new Vector2(-610f, -120f)));
        Nodes.Add(new TrainTrackNode("switch", new Vector2(-150f, -120f)));
        Nodes.Add(new TrainTrackNode("platform", new Vector2(460f, 145f)));
        Nodes.Add(new TrainTrackNode("cargo", new Vector2(460f, -365f)));
        Nodes.Add(new TrainTrackNode("city", new Vector2(720f, 270f)));
        Nodes.Add(new TrainTrackNode("quarry", new Vector2(720f, -395f)));

        Segments.Add(new TrainTrackSegment("depot-switch", 0, 1));
        Segments.Add(new TrainTrackSegment("switch-platform", 1, 2));
        Segments.Add(new TrainTrackSegment("switch-cargo", 1, 3));
        Segments.Add(new TrainTrackSegment("platform-city", 2, 4));
        Segments.Add(new TrainTrackSegment("cargo-quarry", 3, 5));

        Stations.Add(new TrainStationRuntime
        {
            id = "depot",
            title = "Депо",
            kind = TrainStationKind.Depot,
            position = new Vector2(-610f, -28f),
            acceptsPassengers = true,
            handlesCargo = true,
            waitingPassengers = 0,
            passengerDestinationId = "platform"
        });
        Stations.Add(new TrainStationRuntime
        {
            id = "platform",
            title = "Платформа",
            kind = TrainStationKind.PassengerPlatform,
            position = new Vector2(470f, 240f),
            acceptsPassengers = true,
            handlesCargo = false,
            waitingPassengers = 2,
            passengerDestinationId = "depot"
        });
        Stations.Add(new TrainStationRuntime
        {
            id = "cargo",
            title = "Грузовая",
            kind = TrainStationKind.CargoStation,
            position = new Vector2(455f, -270f),
            acceptsPassengers = false,
            handlesCargo = true,
            waitingPassengers = 0,
            passengerDestinationId = string.Empty,
            cargoId = "wood",
            cargoTitle = "РґРµСЂРµРІРѕ",
            cargoColor = new Color(0.55f, 0.32f, 0.12f)
        });

        Stations.Add(new TrainStationRuntime
        {
            id = "city",
            title = "Р“РѕСЂРѕРґ",
            kind = TrainStationKind.PassengerPlatform,
            position = new Vector2(625f, 335f),
            acceptsPassengers = true,
            handlesCargo = false,
            waitingPassengers = 1,
            passengerDestinationId = "platform"
        });

        Stations.Add(new TrainStationRuntime
        {
            id = "quarry",
            title = "РљР°СЂСЊРµСЂ",
            kind = TrainStationKind.CargoStation,
            position = new Vector2(625f, -345f),
            acceptsPassengers = false,
            handlesCargo = true,
            waitingPassengers = 0,
            passengerDestinationId = string.Empty,
            cargoId = "stone",
            cargoTitle = "РєР°РјРµРЅСЊ",
            cargoColor = new Color(0.45f, 0.47f, 0.48f)
        });

        GetStation("depot").carSlots.Add(new Vector2(-615f, -300f));
        GetStation("depot").carSlots.Add(new Vector2(-505f, -300f));
        GetStation("platform").carSlots.Add(new Vector2(270f, 255f));
        GetStation("platform").carSlots.Add(new Vector2(390f, 315f));
        GetStation("cargo").carSlots.Add(new Vector2(275f, -285f));
        GetStation("cargo").carSlots.Add(new Vector2(395f, -340f));
        GetStation("city").carSlots.Add(new Vector2(600f, 350f));
        GetStation("city").carSlots.Add(new Vector2(710f, 390f));
        GetStation("quarry").carSlots.Add(new Vector2(600f, -365f));
        GetStation("quarry").carSlots.Add(new Vector2(710f, -405f));

        AddPassenger("p1", "platform", "depot", new Color(0.95f, 0.26f, 0.26f));
        AddPassenger("p2", "platform", "depot", new Color(0.22f, 0.52f, 1f));
        AddPassenger("p3", "city", "platform", new Color(0.96f, 0.75f, 0.16f));
    }

    public TrainStationRuntime GetStation(string id)
    {
        for (int i = 0; i < Stations.Count; i++)
        {
            if (Stations[i].id == id)
            {
                return Stations[i];
            }
        }

        return null;
    }

    public Vector2 GetSegmentPoint(int segmentIndex, float progress)
    {
        TrainTrackSegment segment = Segments[Mathf.Clamp(segmentIndex, 0, Segments.Count - 1)];
        Vector2 from = Nodes[segment.fromNode].position;
        Vector2 to = Nodes[segment.toNode].position;
        return Vector2.Lerp(from, to, Mathf.Clamp01(progress));
    }

    public int GetNextSegment(int currentSegment, int moveDirection, out float nextProgress)
    {
        nextProgress = 0f;

        if (moveDirection > 0 && currentSegment == DepotToSwitchSegment)
        {
            return MainSwitch.upperBranch ? SwitchToPlatformSegment : SwitchToCargoSegment;
        }

        if (moveDirection > 0 && currentSegment == SwitchToPlatformSegment)
        {
            return PlatformToCitySegment;
        }

        if (moveDirection > 0 && currentSegment == SwitchToCargoSegment)
        {
            return CargoToQuarrySegment;
        }

        if (moveDirection < 0 && currentSegment == PlatformToCitySegment)
        {
            nextProgress = 1f;
            return SwitchToPlatformSegment;
        }

        if (moveDirection < 0 && currentSegment == CargoToQuarrySegment)
        {
            nextProgress = 1f;
            return SwitchToCargoSegment;
        }

        if (moveDirection < 0 && (currentSegment == SwitchToPlatformSegment || currentSegment == SwitchToCargoSegment))
        {
            nextProgress = 1f;
            return DepotToSwitchSegment;
        }

        return -1;
    }

    public bool TryGetNearbyStation(Vector2 point, float radius, out TrainStationRuntime station)
    {
        station = null;
        float bestDistance = radius;
        for (int i = 0; i < Stations.Count; i++)
        {
            float distance = Vector2.Distance(point, Stations[i].position);
            if (distance <= bestDistance)
            {
                station = Stations[i];
                bestDistance = distance;
            }
        }

        return station != null;
    }

    private void AddPassenger(string id, string stationId, string destinationStationId, Color routeColor)
    {
        TrainStationRuntime station = GetStation(stationId);
        if (station == null)
        {
            return;
        }

        TrainPassengerRuntime passenger = new TrainPassengerRuntime
        {
            id = id,
            currentStationId = stationId,
            destinationStationId = destinationStationId,
            location = TrainPassengerLocation.Station,
            routeColor = routeColor
        };
        station.waitingPassengerList.Add(passenger);
        station.waitingPassengers = station.waitingPassengerList.Count;
    }
}

public class TextMeshLabel
{
    public UnityEngine.UI.Text title;
    public UnityEngine.UI.Text detail;
}
