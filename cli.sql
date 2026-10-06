CREATE DATABASE clinical;
GO

USE clinical;
GO

-- Drop tables in reverse dependency order
DROP TABLE IF EXISTS Payment;
DROP TABLE IF EXISTS Appointment;
DROP TABLE IF EXISTS Schedule;
DROP TABLE IF EXISTS Patient;
DROP TABLE IF EXISTS Doctor;
GO


-- =========================
-- Doctor
-- =========================
CREATE TABLE Doctor
(
    DoctorId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Specialization NVARCHAR(100),
    Phone NVARCHAR(20)
);
GO


-- =========================
-- Patient
-- =========================
CREATE TABLE Patient
(
    PatientId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Age INT NOT NULL,
    Phone NVARCHAR(20) NOT NULL,
    Email NVARCHAR(150) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(500) NOT NULL,
    MedicalHistory NVARCHAR(MAX),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
);
GO


-- =========================
-- Schedule
-- =========================
CREATE TABLE Schedule
(
    ScheduleId INT IDENTITY(1,1) PRIMARY KEY,

    DoctorId INT NOT NULL,

    DayOfWeek NVARCHAR(20) NOT NULL,
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,

    MaxPatients INT NOT NULL,

    CONSTRAINT FK_Schedule_Doctor
        FOREIGN KEY (DoctorId)
        REFERENCES Doctor(DoctorId),

    CONSTRAINT CK_Schedule_MaxPatients
        CHECK (MaxPatients > 0),

    CONSTRAINT CK_Schedule_Time
        CHECK (StartTime < EndTime)
);
GO


-- =========================
-- Appointment
-- =========================
CREATE TABLE Appointment
(
    AppointmentId INT IDENTITY(1,1) PRIMARY KEY,

    PatientId INT NOT NULL,
    ScheduleId INT NOT NULL,

    AppointmentDate DATE NOT NULL,

    Status NVARCHAR(30) NOT NULL,

    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),

    PaymentDeadline DATETIME2 NULL,

    CONSTRAINT FK_Appointment_Patient
        FOREIGN KEY (PatientId)
        REFERENCES Patient(PatientId),

    CONSTRAINT FK_Appointment_Schedule
        FOREIGN KEY (ScheduleId)
        REFERENCES Schedule(ScheduleId),

    CONSTRAINT CK_Appointment_Status
        CHECK (Status IN
        (
            'PendingPayment',
            'Confirmed',
            'Expired',
            'Cancelled',
            'Completed'
        ))
);
GO


-- =========================
-- Payment
-- =========================
CREATE TABLE Payment
(
    PaymentId INT IDENTITY(1,1) PRIMARY KEY,

    AppointmentId INT NOT NULL UNIQUE,

    Amount DECIMAL(10,2) NOT NULL,

    Status NVARCHAR(20) NOT NULL,

    TransactionId NVARCHAR(200) NULL,

    PaidAt DATETIME2 NULL,

    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Payment_Appointment
        FOREIGN KEY (AppointmentId)
        REFERENCES Appointment(AppointmentId),

    CONSTRAINT CK_Payment_Amount
        CHECK (Amount > 0),

    CONSTRAINT CK_Payment_Status
        CHECK (Status IN
        (
            'Pending',
            'Paid',
            'Failed',
            'Expired'
        ))
);
GO
