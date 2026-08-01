using System.Collections.Generic;
using UnityEngine;

public class TrainCarRuntime
{
    public string id;
    public string title;
    public TrainCarType type;
    public Vector2 position;
    public string stationId;
    public string homeStationId;
    public bool coupled;
    public int cargoCount;
    public string cargoId;
    public string cargoTitle;
    public Color cargoColor;
    public int passengerCount;
    public string passengerDestinationId;
    public readonly List<TrainPassengerRuntime> passengers = new List<TrainPassengerRuntime>();
    public readonly List<TrainCargoRuntime> cargos = new List<TrainCargoRuntime>();
    public RectTransform rect;
    public RectTransform contentRoot;
    public string spriteKey;
}

public class TrainCargoRuntime
{
    public string id;
    public string title;
    public Color color;
}

public class TrainVehicleController : MonoBehaviour
{
    private const int MaxTrailPoints = 900;

    private readonly List<Vector2> movementTrail = new List<Vector2>();
    private TrainMapController map;
    private TrainGameConfig config;
    private bool useTapStopDeceleration;
    private int lastTravelDirection = 1;

    public int CurrentSegment { get; private set; }
    public float Progress { get; private set; }
    public int TargetDirection { get; private set; }
    public float CurrentSpeed { get; private set; }
    public Vector2 Position { get; private set; }
    public RectTransform LocomotiveRect { get; set; }
    public readonly List<TrainCarRuntime> FreeCars = new List<TrainCarRuntime>();
    public readonly List<TrainCarRuntime> CoupledCars = new List<TrainCarRuntime>();

    public int MoveDirection => CurrentSpeed > StopThreshold ? 1 : CurrentSpeed < -StopThreshold ? -1 : 0;
    public bool IsStopped => Mathf.Abs(CurrentSpeed) <= StopThreshold && TargetDirection == 0;
    public bool IsBraking => TargetDirection == 0 && Mathf.Abs(CurrentSpeed) > StopThreshold;
    public bool IsAccelerating => TargetDirection != 0 && Mathf.Abs(CurrentSpeed) + StopThreshold < Mathf.Abs(GetTargetSpeed());
    public float SpeedAbs => Mathf.Abs(CurrentSpeed);

    private float StopThreshold => ConfigValue(config != null ? config.nearStopSpeedThreshold : 5f, 5f);

    public void Initialize(TrainMapController mapController, TrainGameConfig trainConfig)
    {
        map = mapController;
        config = trainConfig;
        CurrentSegment = TrainMapController.DepotToSwitchSegment;
        Progress = 0.08f;
        TargetDirection = 0;
        CurrentSpeed = 0f;
        Position = map.GetSegmentPoint(CurrentSegment, Progress);
        movementTrail.Clear();
        movementTrail.Add(Position);
    }

    public void SetMoveDirection(int direction)
    {
        TargetDirection = Mathf.Clamp(direction, -1, 1);
        if (TargetDirection != 0)
        {
            lastTravelDirection = TargetDirection;
        }
    }

    public void Stop()
    {
        TargetDirection = 0;
    }

    public void SmoothStop(bool fromTap = false)
    {
        TargetDirection = 0;
        useTapStopDeceleration = fromTap;
    }

    public void RequestForward()
    {
        if (TargetDirection < 0 || CurrentSpeed < -StopThreshold)
        {
            SmoothStop();
            return;
        }

        TargetDirection = 1;
        lastTravelDirection = 1;
    }

    public void RequestBack()
    {
        if (TargetDirection > 0 || CurrentSpeed > StopThreshold)
        {
            SmoothStop();
            return;
        }

        if (IsStopped)
        {
            TargetDirection = -1;
            lastTravelDirection = -1;
            return;
        }

        SmoothStop();
    }

    public void ResumeLastDirection()
    {
        TargetDirection = lastTravelDirection == 0 ? 1 : lastTravelDirection;
    }

    public void Tick(float deltaTime)
    {
        if (map == null)
        {
            return;
        }

        float targetSpeed = GetTargetSpeed();
        float brakeRate = useTapStopDeceleration
            ? ConfigValue(config != null ? config.stopTapDeceleration : 360f, 360f)
            : ConfigValue(config != null ? config.brakeDeceleration : 260f, 260f);
        float rate = TargetDirection == 0 || Mathf.Sign(CurrentSpeed) != Mathf.Sign(targetSpeed) && Mathf.Abs(CurrentSpeed) > StopThreshold
            ? brakeRate
            : ConfigValue(config != null ? config.acceleration : 140f, 140f);
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * deltaTime);
        if (Mathf.Abs(CurrentSpeed) <= StopThreshold && TargetDirection == 0)
        {
            CurrentSpeed = 0f;
            useTapStopDeceleration = false;
        }

        if (Mathf.Abs(CurrentSpeed) <= 0.01f)
        {
            UpdateCoupledCarPositions(deltaTime);
            return;
        }

        Vector2 before = Position;
        float segmentLength = Mathf.Max(1f, Vector2.Distance(map.GetSegmentPoint(CurrentSegment, 0f), map.GetSegmentPoint(CurrentSegment, 1f)));
        Progress += CurrentSpeed * deltaTime / segmentLength;

        if (Progress > 1f)
        {
            float nextProgress;
            int nextSegment = map.GetNextSegment(CurrentSegment, 1, out nextProgress);
            if (nextSegment >= 0)
            {
                CurrentSegment = nextSegment;
                Progress = Mathf.Clamp01(Progress - 1f + nextProgress);
            }
            else
            {
                Progress = 1f;
                Stop();
                CurrentSpeed = 0f;
            }
        }
        else if (Progress < 0f)
        {
            float nextProgress;
            int nextSegment = map.GetNextSegment(CurrentSegment, -1, out nextProgress);
            if (nextSegment >= 0)
            {
                CurrentSegment = nextSegment;
                Progress = Mathf.Clamp01(nextProgress + Progress);
            }
            else
            {
                Progress = 0f;
                Stop();
                CurrentSpeed = 0f;
            }
        }

        Position = map.GetSegmentPoint(CurrentSegment, Progress);
        if (Vector2.Distance(before, Position) > 0.2f)
        {
            movementTrail.Add(Position);
            if (movementTrail.Count > MaxTrailPoints)
            {
                movementTrail.RemoveAt(0);
            }
        }

        UpdateCoupledCarPositions(deltaTime);
    }

    public string GetMotionStateLabel()
    {
        if (IsStopped)
        {
            return "стоит";
        }

        if (IsBraking)
        {
            return "тормозит";
        }

        if (IsAccelerating)
        {
            return "разгон";
        }

        return CurrentSpeed >= 0f ? "вперёд" : "назад";
    }

    public Vector2 GetTailPosition()
    {
        if (CoupledCars.Count == 0)
        {
            return Position;
        }

        return CoupledCars[CoupledCars.Count - 1].position;
    }

    public bool TryFindNearestFreeCar(float radius, out TrainCarRuntime nearest)
    {
        nearest = null;
        float bestDistance = radius;
        Vector2 tail = GetTailPosition();
        for (int i = 0; i < FreeCars.Count; i++)
        {
            if (FreeCars[i].coupled)
            {
                continue;
            }

            float distance = Vector2.Distance(tail, FreeCars[i].position);
            if (distance <= bestDistance)
            {
                nearest = FreeCars[i];
                bestDistance = distance;
            }
        }

        return nearest != null;
    }

    public bool TryFindNearestFreeCarAtStation(TrainStationRuntime station, out TrainCarRuntime nearest)
    {
        nearest = null;
        if (station == null)
        {
            return false;
        }

        float bestDistance = float.MaxValue;
        for (int i = 0; i < FreeCars.Count; i++)
        {
            TrainCarRuntime car = FreeCars[i];
            if (car.coupled || car.stationId != station.id)
            {
                continue;
            }

            float distance = Vector2.Distance(station.position, car.position);
            if (distance < bestDistance)
            {
                nearest = car;
                bestDistance = distance;
            }
        }

        return nearest != null;
    }

    public bool CoupleNearest(float radius, out TrainCarRuntime car)
    {
        car = null;
        if (!IsStopped || !TryFindNearestFreeCar(radius, out car))
        {
            return false;
        }

        car.coupled = true;
        car.stationId = string.Empty;
        FreeCars.Remove(car);
        CoupledCars.Add(car);
        ResetTrailForCurrentConsist();
        UpdateCoupledCarPositions(0f, true);
        return true;
    }

    public bool CoupleFromStation(TrainStationRuntime station, out TrainCarRuntime car)
    {
        car = null;
        if (!IsStopped || !TryFindNearestFreeCarAtStation(station, out car))
        {
            return false;
        }

        car.coupled = true;
        car.stationId = string.Empty;
        FreeCars.Remove(car);
        CoupledCars.Add(car);
        ResetTrailForCurrentConsist();
        UpdateCoupledCarPositions(0f, true);
        return true;
    }

    public bool UncoupleLast(out TrainCarRuntime car)
    {
        car = null;
        if (!IsStopped || CoupledCars.Count == 0)
        {
            return false;
        }

        car = CoupledCars[CoupledCars.Count - 1];
        CoupledCars.RemoveAt(CoupledCars.Count - 1);
        car.coupled = false;
        car.position = GetTailPosition() + new Vector2(-70f, -45f);
        FreeCars.Add(car);
        return true;
    }

    public bool UncoupleLastAtStation(TrainStationRuntime station, out TrainCarRuntime car)
    {
        car = null;
        if (!IsStopped || station == null || CoupledCars.Count == 0)
        {
            return false;
        }

        car = CoupledCars[CoupledCars.Count - 1];
        CoupledCars.RemoveAt(CoupledCars.Count - 1);
        car.coupled = false;
        car.stationId = station.id;
        car.position = GetFreeStationSlot(station);
        FreeCars.Add(car);
        ResetTrailForCurrentConsist();
        UpdateCoupledCarPositions(0f, true);
        return true;
    }

    public bool HasCarType(TrainCarType type)
    {
        for (int i = 0; i < CoupledCars.Count; i++)
        {
            if (CoupledCars[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    public TrainCarRuntime FindFirstCar(TrainCarType type)
    {
        for (int i = 0; i < CoupledCars.Count; i++)
        {
            if (CoupledCars[i].type == type)
            {
                return CoupledCars[i];
            }
        }

        return null;
    }

    private void UpdateCoupledCarPositions(float deltaTime = 0f, bool snap = false)
    {
        float spacing = GetCarSpacing();
        float spring = ConfigValue(config != null ? config.carSpring : 9f, 9f);
        float t = snap ? 1f : 1f - Mathf.Exp(-spring * Mathf.Max(0.001f, deltaTime));
        for (int i = 0; i < CoupledCars.Count; i++)
        {
            Vector2 target = GetTrailPosition(spacing * (i + 1));
            CoupledCars[i].position = Vector2.Lerp(CoupledCars[i].position, target, t);
        }
    }

    private float GetTargetSpeed()
    {
        if (TargetDirection > 0)
        {
            return ConfigValue(config != null ? config.trainSpeed : 190f, 190f);
        }

        if (TargetDirection < 0)
        {
            return -ConfigValue(config != null ? config.reverseSpeed : 145f, 145f);
        }

        return 0f;
    }

    private Vector2 GetFreeStationSlot(TrainStationRuntime station)
    {
        if (station.carSlots.Count == 0)
        {
            return station.position + new Vector2(0f, -85f);
        }

        for (int i = 0; i < station.carSlots.Count; i++)
        {
            bool occupied = false;
            for (int j = 0; j < FreeCars.Count; j++)
            {
                if (FreeCars[j].stationId == station.id && Vector2.Distance(FreeCars[j].position, station.carSlots[i]) < 24f)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
            {
                return station.carSlots[i];
            }
        }

        return station.carSlots[station.carSlots.Count - 1] + new Vector2(0f, -70f);
    }

    private void ResetTrailForCurrentConsist()
    {
        movementTrail.Clear();
        Vector2 direction = GetCurrentSegmentDirection();
        if (TargetDirection < 0 || CurrentSpeed < -StopThreshold)
        {
            direction *= -1f;
        }

        float spacing = GetCarSpacing();
        for (int i = 0; i < CoupledCars.Count + 5; i++)
        {
            movementTrail.Add(Position - direction * spacing * (CoupledCars.Count + 5 - i));
        }

        movementTrail.Add(Position);
    }

    private Vector2 GetCurrentSegmentDirection()
    {
        if (map == null)
        {
            return Vector2.right;
        }

        Vector2 start = map.GetSegmentPoint(CurrentSegment, 0f);
        Vector2 end = map.GetSegmentPoint(CurrentSegment, 1f);
        Vector2 direction = end - start;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
    }

    private Vector2 GetTrailPosition(float distanceBehind)
    {
        if (movementTrail.Count < 2)
        {
            return Position - new Vector2(distanceBehind, 0f);
        }

        float covered = 0f;
        Vector2 previous = Position;
        for (int i = movementTrail.Count - 1; i >= 0; i--)
        {
            Vector2 current = movementTrail[i];
            float segment = Vector2.Distance(previous, current);
            if (covered + segment >= distanceBehind)
            {
                float t = Mathf.InverseLerp(covered, covered + segment, distanceBehind);
                return Vector2.Lerp(previous, current, t);
            }

            covered += segment;
            previous = current;
        }

        return movementTrail[0];
    }

    private float GetCarSpacing()
    {
        float spacing = ConfigValue(config != null ? config.carSpacing : 132f, 132f);
        float locomotiveWidth = LocomotiveRect != null ? LocomotiveRect.rect.width : 116f;
        float carWidth = 104f;
        for (int i = 0; i < CoupledCars.Count; i++)
        {
            if (CoupledCars[i].rect != null)
            {
                carWidth = Mathf.Max(carWidth, CoupledCars[i].rect.rect.width);
            }
        }

        return Mathf.Max(spacing, Mathf.Max(locomotiveWidth, carWidth) + 26f);
    }

    private static float ConfigValue(float value, float fallback)
    {
        return value > 0f ? value : fallback;
    }
}
