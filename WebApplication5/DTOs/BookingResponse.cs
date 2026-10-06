namespace WebApplication5.DTOs
{
    public class BookingResponseDto
    {
        public int AppointmentId { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? PaymentDeadline { get; set; }
        public decimal Amount { get; set; }
    }
}
