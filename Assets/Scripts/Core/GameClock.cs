using System;
using UnityEngine;

namespace MyCity.Core
{
    /// <summary>
    /// Единственный источник игрового времени для всех систем города.
    /// При скорости 1x одна игровая минута проходит за 60 реальных секунд.
    /// </summary>
    public sealed class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        [SerializeField, Min(1f)] private float realSecondsPerGameMinute = 60f;
        [SerializeField, Min(1)] private int minutesPerDay = 1440;
        [SerializeField, Min(1)] private int daysPerMonth = 30;
        [SerializeField, Min(1)] private int monthsPerYear = 12;

        public long Tick { get; private set; }
        public int MinuteOfDay { get; private set; } = 8 * 60;
        public int Day { get; private set; } = 1; // Непрерывный номер игрового дня.
        public int Month { get; private set; } = 1;
        public int Year { get; private set; } = 1;
        public bool IsPaused { get; private set; }
        public float SpeedMultiplier { get; private set; } = 1f;
        public float TimeOfDayHours { get { return MinuteOfDay / 60f; } }

        public event Action<long> OnTick;
        public event Action<int> OnDayChanged;
        private float accumulator;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (IsPaused || SpeedMultiplier <= 0f || realSecondsPerGameMinute <= 0f)
                return;

            accumulator += Time.unscaledDeltaTime * SpeedMultiplier;
            while (accumulator >= realSecondsPerGameMinute)
            {
                accumulator -= realSecondsPerGameMinute;
                AdvanceOneMinute();
            }
        }

        private void AdvanceOneMinute()
        {
            Tick++;
            MinuteOfDay++;

            if (MinuteOfDay >= minutesPerDay)
            {
                MinuteOfDay = 0;
                Day++;

                if ((Day - 1) % daysPerMonth == 0)
                {
                    Month++;
                    if (Month > monthsPerYear)
                    {
                        Month = 1;
                        Year++;
                    }
                }

                Action<int> dayHandler = OnDayChanged;
                if (dayHandler != null) dayHandler(Day);
                GameEventBus.Publish(new GameEvent(
                    GameEventType.DayChanged, Tick, "clock", null, Day.ToString()));
            }

            Action<long> handler = OnTick;
            if (handler != null) handler(Tick);
        }

        public void SetPaused(bool paused) { IsPaused = paused; }

        public void SetSpeed(float multiplier)
        {
            SpeedMultiplier = Mathf.Clamp(multiplier, 0f, 32f);
        }

        public void SetTime(int day, int minuteOfDay, long tick = -1)
        {
            Day = Math.Max(1, day);
            MinuteOfDay = Mathf.Clamp(minuteOfDay, 0, minutesPerDay - 1);
            Tick = tick >= 0 ? tick : (Day - 1L) * minutesPerDay + MinuteOfDay;
            Month = ((Day - 1) / daysPerMonth) % monthsPerYear + 1;
            Year = ((Day - 1) / (daysPerMonth * monthsPerYear)) + 1;
            accumulator = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
