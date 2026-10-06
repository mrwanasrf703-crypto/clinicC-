namespace WebApplication5.DTOs
{
    public class ConfirmPaymentDto
    {
        public int AppointmentId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
