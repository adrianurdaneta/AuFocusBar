using System;

namespace AuTaskBar.Services
{
    public class CalendarService : ICalendarService
    {
        public (string Title, DateTime Start)? GetNextMeeting()
        {
            var start = DateTime.Now.AddMinutes(12);
            return ("Sprint Sync", start);
        }
    }
}
