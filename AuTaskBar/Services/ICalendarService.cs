using System;

namespace AuTaskBar.Services
{
    public interface ICalendarService
    {
        (string Title, DateTime Start)? GetNextMeeting();
    }
}
