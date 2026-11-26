namespace MyCity.Simulation
{
    public enum CitizenDailyPhase
    {
        Sleep = 0,
        Morning = 1,
        Work = 2,
        Evening = 3
    }

    /// <summary>Shared city-clock schedule: sleep 22:00-06:00, morning 06:00-09:00, work 09:00-17:00, evening 17:00-22:00.</summary>
    public static class CitizenDailySchedule
    {
        public static CitizenDailyPhase GetPhase(int minuteOfDay)
        {
            int minute = minuteOfDay % 1440;
            if (minute < 0) minute += 1440;
            if (minute < 360 || minute >= 1320) return CitizenDailyPhase.Sleep;
            if (minute < 540) return CitizenDailyPhase.Morning;
            if (minute < 1020) return CitizenDailyPhase.Work;
            return CitizenDailyPhase.Evening;
        }

        public static string GetLabel(CitizenDailyPhase phase)
        {
            switch (phase)
            {
                case CitizenDailyPhase.Sleep: return "sleep";
                case CitizenDailyPhase.Morning: return "morning";
                case CitizenDailyPhase.Work: return "work";
                case CitizenDailyPhase.Evening: return "evening";
                default: return "unknown";
            }
        }
    }
}
