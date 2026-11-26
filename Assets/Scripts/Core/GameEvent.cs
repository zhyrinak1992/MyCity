using System;

namespace MyCity.Core
{
    public enum GameEventType
    {
        DayChanged,
        BuildingPlaced,
        BuildingRemoved,
        CitizenStateChanged,
        PassengerFlowChanged,
        FlightPhaseChanged,
        InternationalRouteChanged,
        InternationalDemandUpdated,
        EmploymentChanged,
        WorkAttendanceChanged,
        SaveCompleted,
        SaveFailed
    }

    [Serializable]
    public sealed class GameEvent
    {
        public GameEventType Type;
        public long Tick;
        public string SourceId;
        public string TargetId;
        public string Payload;

        public GameEvent(GameEventType type, long tick, string sourceId, string targetId, string payload)
        {
            Type = type;
            Tick = tick;
            SourceId = sourceId;
            TargetId = targetId;
            Payload = payload;
        }
    }

    public static class GameEventBus
    {
        public static event Action<GameEvent> Published;

        public static void Publish(GameEvent gameEvent)
        {
            if (gameEvent == null) return;
            Action<GameEvent> handler = Published;
            if (handler != null) handler(gameEvent);
        }
    }
}
