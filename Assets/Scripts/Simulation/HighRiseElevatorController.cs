using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Minute-driven elevator dispatch for one tower. Requests are FIFO, cars have finite capacity and move between real floor heights.</summary>
    public sealed class HighRiseElevatorController
    {
        private sealed class Request { public int From; public int To; public int Passengers; }
        private sealed class Car
        {
            public Transform Visual;
            public int Floor = 1;
            public int TargetFloor = 1;
            public int Passengers;
            public int Capacity = 8;
            public int TravelMinutes;
            public readonly Queue<Request> Queue = new Queue<Request>();
        }

        private readonly List<Car> cars = new List<Car>();
        private readonly float floorHeight;
        private readonly float shaftX;
        private readonly float shaftZ;
        private readonly Transform root;
        private readonly Action<int> onTrip;
        public int CompletedTrips { get; private set; }
        public int WaitingRequests { get; private set; }
        public int PassengersInCars { get; private set; }

        public HighRiseElevatorController(Transform interiorRoot, int carCount, int floors, float heightPerFloor, float x, float z, Material carMaterial, Action<int> tripCallback)
        {
            root = interiorRoot; floorHeight = Mathf.Max(1f, heightPerFloor); shaftX = x; shaftZ = z; onTrip = tripCallback;
            int count = Mathf.Clamp(carCount, 1, 8);
            for (int i = 0; i < count; i++)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Elevator cabin " + (i + 1);
                visual.transform.SetParent(root, false);
                visual.transform.localScale = new Vector3(.92f, 1.9f, .92f);
                visual.transform.localPosition = new Vector3(shaftX + (i - (count - 1) * .5f) * 1.05f, .95f, shaftZ);
                Renderer renderer = visual.GetComponent<Renderer>(); if (renderer != null && carMaterial != null) renderer.sharedMaterial = carMaterial;
                Collider collider = visual.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.Destroy(collider);
                cars.Add(new Car { Visual = visual.transform });
            }
        }

        public bool Enqueue(int fromFloor, int toFloor, int passengers)
        {
            if (cars.Count == 0 || passengers <= 0 || fromFloor == toFloor) return false;
            Request request = new Request { From = Mathf.Max(1, fromFloor), To = Mathf.Max(1, toFloor), Passengers = Mathf.Clamp(passengers, 1, 8) };
            Car best = cars[0];
            for (int i = 1; i < cars.Count; i++) if (cars[i].Queue.Count < best.Queue.Count) best = cars[i];
            best.Queue.Enqueue(request); WaitingRequests++; return true;
        }

        public void AdvanceOneMinute(int maximumFloor)
        {
            foreach (Car car in cars)
            {
                if (car.TravelMinutes > 0) { car.TravelMinutes--; if (car.TravelMinutes == 0) Arrive(car); }
                if (car.TravelMinutes == 0 && car.Queue.Count > 0)
                {
                    Request next = car.Queue.Peek();
                    if (car.Floor != next.From) Move(car, next.From, maximumFloor);
                    else { car.Queue.Dequeue(); WaitingRequests = Mathf.Max(0, WaitingRequests - 1); car.Passengers = next.Passengers; Move(car, next.To, maximumFloor); }
                }
                if (car.Visual != null)
                {
                    Vector3 p = car.Visual.localPosition;
                    float targetY = (car.TargetFloor - 1) * floorHeight + .95f;
                    p.y = Mathf.MoveTowards(p.y, targetY, floorHeight / 2f);
                    car.Visual.localPosition = p;
                }
            }
            PassengersInCars = 0; foreach (Car car in cars) PassengersInCars += car.Passengers;
        }

        private void Move(Car car, int target, int maxFloor)
        {
            car.TargetFloor = Mathf.Clamp(target, 1, Mathf.Max(1, maxFloor));
            car.TravelMinutes = Mathf.Max(1, Mathf.Abs(car.TargetFloor - car.Floor) * 2);
        }
        private void Arrive(Car car)
        {
            car.Floor = car.TargetFloor;
            if (car.Passengers > 0) { CompletedTrips++; if (onTrip != null) onTrip(1); car.Passengers = 0; }
        }
    }
}
