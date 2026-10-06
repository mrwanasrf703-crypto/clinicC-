namespace WebApplication5.DTOs
{
    public class AvailableDateDto
    {
        public DateOnly Date { get; set; }
        public string Day { get; set; } = string.Empty;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int MaxPatients { get; set; }
        public int CurrentPatients { get; set; }
        public int RemainingPatients { get; set; }
        public bool IsComplete { get; set; }
        
    }
}
