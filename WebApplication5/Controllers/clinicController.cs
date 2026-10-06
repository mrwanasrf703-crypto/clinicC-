using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebApplication5.DTOs;
using WebApplication5.Models;

namespace WebApplication5.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class clinicController : ControllerBase
    {

        private readonly clinicalContext _context;
        private readonly IConfiguration _configuration;

        public clinicController(clinicalContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            var existingPatient = await _context.Patients
                .FirstOrDefaultAsync(x => x.Email == dto.Email);

            if (existingPatient != null)
            {
                return BadRequest("Email already exists.");
            }

            var patient = new Patient
            {
                Name = dto.Name,
                Age = dto.Age,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            return Ok("Patient registered successfully.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> login(login dto)
        {
            var patient = await _context.Patients
                .FirstOrDefaultAsync(x => x.Email == dto.Email);

            if (patient == null || !BCrypt.Net.BCrypt.Verify(dto.Password, patient.PasswordHash))
            {
                return Unauthorized("Invalid email or password.");
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, patient.PatientId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, patient.Email)
             };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                message = "Login successful.",
                token = jwt
            });
        }

        /*==============================================================================*/


        [Authorize]
        [HttpGet("available-dates")]
        public async Task<IActionResult> GetAvailableDates()
        {
            var schedules = await _context.Schedules
                .ToListAsync();

            var appointments = await _context.Appointments
                .Where(x =>
                    x.Status == "Confirmed" ||
                    (x.Status == "PendingPayment" &&
                     x.PaymentDeadline > DateTime.Now))
                .ToListAsync();

            var result = new List<AvailableDateDto>();

            var today = DateOnly.FromDateTime(DateTime.Today);

            for (int i = 0; i < 7; i++)
            {
                var date = today.AddDays(i);

                var dayName = date.DayOfWeek.ToString();

                var schedule = schedules
                    .FirstOrDefault(x => x.DayOfWeek == dayName);

                if (schedule == null)
                    continue;

                var currentPatients = appointments.Count(x =>
                    x.ScheduleId == schedule.ScheduleId &&
                    x.AppointmentDate == date);

                var remainingPatients = schedule.MaxPatients - currentPatients;

                result.Add(new AvailableDateDto
                {
                    Date = date,
                    Day = dayName,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime,
                    MaxPatients = schedule.MaxPatients,
                    CurrentPatients = currentPatients,
                    RemainingPatients = remainingPatients > 0 ? remainingPatients : 0,
                    IsComplete = remainingPatients <= 0
                });
            }

            return Ok(result);
        }

        /*==================================================================================*/

        [Authorize]
        [HttpPost("book")]
        public async Task<IActionResult> BookAppointment(BookAppointmentDto dto)
        {
            var patientId = int.Parse(
                User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value!
            );

            var schedule = await _context.Schedules
                .FirstOrDefaultAsync(x => x.ScheduleId == dto.ScheduleId);

            if (schedule == null)
                return NotFound("Schedule not found.");

            if (schedule.DayOfWeek != dto.AppointmentDate.DayOfWeek.ToString())
                return BadRequest("The selected date does not match the schedule.");

            if (dto.AppointmentDate < DateOnly.FromDateTime(DateTime.Today))
                return BadRequest("You cannot book a date in the past.");

            var currentDateTime = DateTime.Now;

            var currentPatients = await _context.Appointments
                .CountAsync(x =>
                    x.ScheduleId == dto.ScheduleId &&
                    x.AppointmentDate == dto.AppointmentDate &&
                    (
                        x.Status == "Confirmed" ||
                        (x.Status == "PendingPayment" &&
                         x.PaymentDeadline > currentDateTime)
                    ));

            if (currentPatients >= schedule.MaxPatients)
                return BadRequest("This day is complete.");

            var existingPendingAppointment = await _context.Appointments
                .FirstOrDefaultAsync(x =>
                    x.PatientId == patientId &&
                    x.AppointmentDate == dto.AppointmentDate &&
                    x.ScheduleId == dto.ScheduleId &&
                    x.Status == "PendingPayment" &&
                    x.PaymentDeadline > currentDateTime);

            if (existingPendingAppointment != null)
                return BadRequest("You already have a pending payment for this date.");

            var paymentDeadline = currentDateTime.AddMinutes(10);

            var appointment = new Appointment
            {
                PatientId = patientId,
                ScheduleId = dto.ScheduleId,
                AppointmentDate = dto.AppointmentDate,
                Status = "PendingPayment",
                CreatedAt = currentDateTime,
                PaymentDeadline = paymentDeadline
            };

            _context.Appointments.Add(appointment);

            await _context.SaveChangesAsync();

            var payment = new Payment
            {
                AppointmentId = appointment.AppointmentId,
                Amount = 500,
                Status = "Pending",
                CreatedAt = currentDateTime
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            return Ok(new BookingResponseDto
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentDate = appointment.AppointmentDate,
                Status = appointment.Status,
                PaymentDeadline = appointment.PaymentDeadline,
                Amount = payment.Amount
            });
        }


        /*==================================================================================*/


        [Authorize]
        [HttpPost("confirm-payment")]
        public async Task<IActionResult> ConfirmPayment(ConfirmPaymentDto dto)
        {
            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(x => x.AppointmentId == dto.AppointmentId);

            if (appointment == null)
                return NotFound("Appointment not found.");

            if (appointment.Status != "PendingPayment")
                return BadRequest("This appointment is not waiting for payment.");

            if (appointment.PaymentDeadline == null ||
                appointment.PaymentDeadline <= DateTime.Now)
            {
                appointment.Status = "Expired";

                var expiredPayment = await _context.Payments
                    .FirstOrDefaultAsync(x => x.AppointmentId == appointment.AppointmentId);

                if (expiredPayment != null)
                    expiredPayment.Status = "Expired";

                await _context.SaveChangesAsync();

                return BadRequest("Payment deadline has expired.");
            }

            var payment = await _context.Payments
                .FirstOrDefaultAsync(x => x.AppointmentId == appointment.AppointmentId);

            if (payment == null)
                return NotFound("Payment not found.");

            payment.Status = "Paid";
            payment.PaidAt = DateTime.Now;

            appointment.Status = "Confirmed";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Payment successful. Appointment confirmed.",
                appointmentId = appointment.AppointmentId,
                paymentStatus = payment.Status,
                appointmentStatus = appointment.Status
            });
        }

        /*====================================================================================*/

        [Authorize]
        [HttpGet("my-appointments")]
        public async Task<IActionResult> GetMyAppointments()
        {
            var patientIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(patientIdClaim))
                return Unauthorized();

            var patientId = int.Parse(patientIdClaim);

            var appointments = await _context.Appointments
                .Where(x => x.PatientId == patientId)
                .Include(x => x.Schedule)
                .Include(x => x.Payment)
                .OrderByDescending(x => x.AppointmentDate)
                .Select(x => new MyAppointmentDto
                {
                    AppointmentId = x.AppointmentId,
                    AppointmentDate = x.AppointmentDate,
                    StartTime = x.Schedule.StartTime,
                    EndTime = x.Schedule.EndTime,
                    AppointmentStatus = x.Status,
                    PaymentStatus = x.Payment.Status,
                    Amount = x.Payment.Amount,
                    PaymentDeadline = x.PaymentDeadline
                })
                .ToListAsync();

            return Ok(appointments);
        }

        /*====================================================================================*/

        [HttpGet("doctor/appointments")]
        public async Task<IActionResult> GetDoctorAppointments()
        {
            var appointments = await _context.Appointments
                .Include(x => x.Patient)
                .Include(x => x.Schedule)
                .Include(x => x.Payment)
                .OrderBy(x => x.AppointmentDate)
                .Select(x => new DoctorAppointmentDto
                {
                    AppointmentId = x.AppointmentId,

                    PatientName = x.Patient.Name,
                    Age = x.Patient.Age,
                    Email = x.Patient.Email,
                    Phone = x.Patient.Phone,
                    MedicalHistory = x.Patient.MedicalHistory,

                    AppointmentDate = x.AppointmentDate,
                    StartTime = x.Schedule.StartTime,
                    EndTime = x.Schedule.EndTime,

                    AppointmentStatus = x.Status,
                    PaymentStatus = x.Payment.Status,
                    Amount = x.Payment.Amount
                })
                .ToListAsync();

            return Ok(appointments);
        }


        /*==========================================================================================*/

    }
}
