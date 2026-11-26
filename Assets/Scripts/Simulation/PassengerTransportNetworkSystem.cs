using System;
using System.Collections.Generic;
using MyCity.Core;
using UnityEngine;

namespace MyCity.Simulation
{
    public enum TransportNodeType { BusTerminal, MetroStation, RailwayStation, Airport }

    [Serializable]
    public sealed class PassengerTransportNode
    {
        public string Id;
        public string Name;
        public TransportNodeType Type;
        public int HourlyCapacity;
        public int BaseDemand;
        public int PassengersPerHour;
        public int TransferCount;
        public float Utilization;
        public bool IsPrototype;
    }

    [Serializable]
    public sealed class PassengerTransferLink
    {
        public string FromNodeId;
        public string ToNodeId;
        public int TransferMinutes;
        public bool IsAvailable;
    }

    /// <summary>
    /// Aggregate passenger-flow prototype. It models demand and interchange load,
    /// not individual pathfinding, vehicle movement, ticketing or completed journeys.
    /// </summary>
    public sealed class PassengerTransportNetworkSystem : MonoBehaviour
    {
        private readonly List<PassengerTransportNode> nodes = new List<PassengerTransportNode>();
        private readonly List<PassengerTransferLink> links = new List<PassengerTransferLink>();
        private GameClock clock;
        private int lastHour = -1;
        private int totalPassengers;
        private int overloadedNodes;

        public IList<PassengerTransportNode> Nodes { get { return nodes.AsReadOnly(); } }
        public IList<PassengerTransferLink> Links { get { return links.AsReadOnly(); } }
        public int TotalPassengersPerHour { get { return totalPassengers; } }
        public int OverloadedNodes { get { return overloadedNodes; } }
        public event Action OnPassengerFlowUpdated;

        private void OnEnable() { TrySubscribe(); }
        private void Update() { if (clock == null) TrySubscribe(); }
        private void OnDisable() { if (clock != null) clock.OnTick -= HandleTick; clock = null; }

        public void InitializeDefaults()
        {
            if (nodes.Count > 0) return;
            AddNode("bus-central", "Центральный автовокзал", TransportNodeType.BusTerminal, 420, 165);
            AddNode("metro-central", "Центральная станция метро", TransportNodeType.MetroStation, 900, 380);
            AddNode("rail-central", "Главный железнодорожный вокзал", TransportNodeType.RailwayStation, 650, 210);
            AddNode("airport-main", "Международный аэропорт", TransportNodeType.Airport, 1100, 290);
            AddLink("bus-central", "metro-central", 6);
            AddLink("metro-central", "rail-central", 8);
            AddLink("rail-central", "airport-main", 14);
            AddLink("bus-central", "rail-central", 11);
            Recalculate(clock != null ? clock.MinuteOfDay : 480, false);
        }

        private void AddNode(string id, string name, TransportNodeType type, int capacity, int demand)
        {
            nodes.Add(new PassengerTransportNode { Id=id, Name=name, Type=type, HourlyCapacity=capacity,
                BaseDemand=demand, IsPrototype=true });
        }

        private void AddLink(string from, string to, int minutes)
        {
            links.Add(new PassengerTransferLink { FromNodeId=from, ToNodeId=to, TransferMinutes=minutes, IsAvailable=true });
        }

        private void TrySubscribe()
        {
            GameClock current = GameClock.Instance;
            if (current == null || current == clock) return;
            if (clock != null) clock.OnTick -= HandleTick;
            clock = current;
            clock.OnTick += HandleTick;
            Recalculate(clock.MinuteOfDay, false);
        }

        private void HandleTick(long tick)
        {
            if (clock == null) return;
            int hour = clock.MinuteOfDay / 60;
            if (hour == lastHour) return;
            Recalculate(clock.MinuteOfDay, true);
        }

        private void Recalculate(int minuteOfDay, bool notify)
        {
            int hour = minuteOfDay / 60;
            lastHour = hour;
            // Deterministic city-wide demand curve: morning/evening peaks, lower night traffic.
            float multiplier = (hour >= 7 && hour <= 9) ? 1.45f :
                (hour >= 16 && hour <= 19) ? 1.6f :
                (hour >= 0 && hour <= 5) ? 0.22f :
                (hour >= 10 && hour <= 15) ? 0.92f : 0.68f;
            totalPassengers = 0;
            overloadedNodes = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                PassengerTransportNode node = nodes[i];
                node.PassengersPerHour = Mathf.RoundToInt(node.BaseDemand * multiplier);
                node.TransferCount = CountLinks(node.Id);
                node.Utilization = node.HourlyCapacity <= 0 ? 0f : (float)node.PassengersPerHour / node.HourlyCapacity;
                totalPassengers += node.PassengersPerHour;
                if (node.Utilization > 1f) overloadedNodes++;
            }
            if (notify)
            {
                Action handler = OnPassengerFlowUpdated;
                if (handler != null) handler();
                GameEventBus.Publish(new GameEvent(GameEventType.PassengerFlowChanged,
                    clock != null ? clock.Tick : 0, "passenger-network", null,
                    totalPassengers.ToString() + ":" + overloadedNodes.ToString()));
            }
        }

        private int CountLinks(string nodeId)
        {
            int count = 0;
            for (int i = 0; i < links.Count; i++)
                if (links[i].IsAvailable && (links[i].FromNodeId == nodeId || links[i].ToNodeId == nodeId)) count++;
            return count;
        }

        public string GetTypeLabel(TransportNodeType type)
        {
            switch (type)
            {
                case TransportNodeType.BusTerminal: return "Автобус";
                case TransportNodeType.MetroStation: return "Метро";
                case TransportNodeType.RailwayStation: return "Ж/д";
                default: return "Авиация";
            }
        }
    }
}
