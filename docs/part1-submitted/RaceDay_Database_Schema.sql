



IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

/* Drop tables if they already exist, in FK-safe order (children first) */
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.EventEnrolments', 'U') IS NOT NULL DROP TABLE dbo.EventEnrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;
GO

/* Safety net: if a previous failed run left named constraints/indexes
   orphaned on some other object, drop them explicitly by name so the
   CREATE TABLE statements below never collide. This makes the script
   safe to re-run even after a partial failure. */
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) +
              N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(kc.name) + N';' + CHAR(10)
FROM sys.key_constraints kc
JOIN sys.tables t ON kc.parent_object_id = t.object_id
WHERE kc.name = 'UQ_Enrolment_Participant_Category';

IF LEN(@sql) > 0 EXEC sp_executesql @sql;
GO

/* ============================================================
   1. Roles
   ============================================================ */
CREATE TABLE dbo.Roles (
    RoleID      INT IDENTITY(1,1) PRIMARY KEY,
    RoleName    NVARCHAR(50) NOT NULL,
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);
GO

/* ============================================================
   2. Users
   ============================================================ */
CREATE TABLE dbo.Users (
    UserID          INT IDENTITY(1,1) PRIMARY KEY,
    RoleID          INT NOT NULL,
    FullName        NVARCHAR(100) NOT NULL,
    Email           NVARCHAR(150) NOT NULL,
    PasswordHash    NVARCHAR(255) NOT NULL,
    PhoneNumber     NVARCHAR(20) NULL,
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleID) REFERENCES dbo.Roles(RoleID)
);
GO

/* ============================================================
   3. Events
   ============================================================ */
CREATE TABLE dbo.Events (
    EventID         INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID     INT NOT NULL,
    EventName       NVARCHAR(150) NOT NULL,
    EventDate       DATE NOT NULL,
    Location        NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(MAX) NULL,
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserID) REFERENCES dbo.Users(UserID)
);
GO

/* ============================================================
   4. Categories
   ============================================================ */
CREATE TABLE dbo.Categories (
    CategoryID      INT IDENTITY(1,1) PRIMARY KEY,
    EventID         INT NOT NULL,
    CategoryName    NVARCHAR(100) NOT NULL,
    DistanceKm      DECIMAL(5,2) NOT NULL,
    MaxParticipants INT NOT NULL DEFAULT 100,
    EntryFee        DECIMAL(8,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID)
);
GO

/* ============================================================
   5. EventEnrolments
   ============================================================ */
CREATE TABLE dbo.EventEnrolments (
    EnrolmentID     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID   INT NOT NULL,
    CategoryID      INT NOT NULL,
    BibNumber       NVARCHAR(10) NULL,
    EnrolmentDate   DATETIME NOT NULL DEFAULT GETDATE(),
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Confirmed',
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantID) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryID) REFERENCES dbo.Categories(CategoryID),
    CONSTRAINT UQ_Enrolment_Participant_Category UNIQUE (ParticipantID, CategoryID)
);
GO

/* ============================================================
   6. Results
   ============================================================ */
CREATE TABLE dbo.Results (
    ResultID        INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID     INT NOT NULL,
    FinishTime      TIME NULL,
    Position        INT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentID),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentID) REFERENCES dbo.EventEnrolments(EnrolmentID)
);
GO

/* ============================================================
   SEED DATA
   ============================================================ */

-- Roles
INSERT INTO dbo.Roles (RoleName) VALUES ('Organiser'), ('Participant'), ('Admin');
GO

-- Users: 2 Organisers, 2 Participants (RoleID 1 = Organiser, 2 = Participant)
INSERT INTO dbo.Users (RoleID, FullName, Email, PasswordHash, PhoneNumber) VALUES
(1, 'Thabo Mokoena',   'thabo.mokoena@raceday.co.za',   'HASH_PLACEHOLDER_1', '0821234567'),
(1, 'Lindiwe Dube',    'lindiwe.dube@raceday.co.za',    'HASH_PLACEHOLDER_2', '0827654321'),
(2, 'Sarah Botha',     'sarah.botha@example.com',       'HASH_PLACEHOLDER_3', '0731112222'),
(2, 'Michael van Wyk', 'michael.vanwyk@example.com',    'HASH_PLACEHOLDER_4', '0733334444');
GO

-- Events: 3 events, owned by the two organisers (UserID 1 and 2)
INSERT INTO dbo.Events (OrganiserID, EventName, EventDate, Location, Description) VALUES
(1, 'Pretoria Spring Marathon',   '2026-10-18', 'Pretoria, Gauteng',   'Annual road race through the Union Buildings precinct.'),
(1, 'Centurion Night Run',        '2026-11-07', 'Centurion, Gauteng',  'Evening fun run with glow-in-the-dark theme.'),
(2, 'Cape Trail Challenge',       '2026-09-26', 'Cape Town, Western Cape', 'Off-road trail event across Table Mountain foothills.');
GO

-- Categories: at least one category per event (three events -> three-plus categories)
INSERT INTO dbo.Categories (EventID, CategoryName, DistanceKm, MaxParticipants, EntryFee) VALUES
(1, '10km Fun Run',    10.00, 500, 150.00),
(1, '42km Marathon',   42.20, 300, 350.00),
(2, '5km Night Run',    5.00, 400, 100.00),
(3, '15km Trail',      15.00, 200, 250.00),
(3, '30km Trail',      30.00, 150, 400.00);
GO

-- EventEnrolments: sample enrolments for the two participants (UserID 3 and 4)
INSERT INTO dbo.EventEnrolments (ParticipantID, CategoryID, BibNumber, Status) VALUES
(3, 1, 'A1001', 'Confirmed'),
(3, 4, 'A1002', 'Confirmed'),
(4, 2, 'A1003', 'Confirmed'),
(4, 3, 'A1004', 'Confirmed');
GO

-- Results: sample results for two of the enrolments
INSERT INTO dbo.Results (EnrolmentID, FinishTime, Position, Status) VALUES
(1, '00:52:14', 12, 'Final'),
(3, '03:45:02', 5,  'Final');
GO