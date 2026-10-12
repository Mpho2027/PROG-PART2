/* ============================================================
   RaceDay database schema - Microsoft SQL Server (SSMS)
   ------------------------------------------------------------
   Version 2 (Part 2). This script produces exactly the same schema as the
   API's EF Core code-first migration (20261012123717_InitialCreate).
   Changes from the Part 1 script are marked [v2] and explained in README.md.

   Running the script DROPS and recreates all RaceDay tables.
   ============================================================ */

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
IF OBJECT_ID('dbo.__EFMigrationsHistory', 'U') IS NOT NULL DROP TABLE dbo.__EFMigrationsHistory;
GO

/* ============================================================
   1. Roles
   ============================================================ */
CREATE TABLE dbo.Roles (
    RoleID      INT IDENTITY(1,1) NOT NULL,
    RoleName    NVARCHAR(50) NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleID)
);
CREATE UNIQUE INDEX UQ_Roles_RoleName ON dbo.Roles (RoleName);
GO

/* ============================================================
   2. Users
   ============================================================ */
CREATE TABLE dbo.Users (
    UserID          INT IDENTITY(1,1) NOT NULL,
    RoleID          INT NOT NULL,
    FullName        NVARCHAR(100) NOT NULL,
    Email           NVARCHAR(150) NOT NULL,
    PasswordHash    NVARCHAR(255) NOT NULL,      -- salted PBKDF2 hash, never the plain password
    PhoneNumber     NVARCHAR(20) NULL,
    CreatedAt       DATETIME NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT PK_Users PRIMARY KEY (UserID),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleID) REFERENCES dbo.Roles (RoleID)
);
CREATE UNIQUE INDEX UQ_Users_Email ON dbo.Users (Email);
CREATE INDEX IX_Users_RoleID ON dbo.Users (RoleID);
GO

/* ============================================================
   3. Events
   ============================================================ */
CREATE TABLE dbo.Events (
    EventID         INT IDENTITY(1,1) NOT NULL,
    OrganiserID     INT NOT NULL,
    EventName       NVARCHAR(150) NOT NULL,
    EventDate       DATE NOT NULL,
    Location        NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(MAX) NULL,
    DistanceKm      DECIMAL(6,2) NOT NULL,       -- [v2] event distance
    EventType       NVARCHAR(10) NOT NULL,       -- [v2] Run / Walk / Cycle
    CreatedAt       DATETIME NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT PK_Events PRIMARY KEY (EventID),
    CONSTRAINT CK_Events_EventType CHECK (EventType IN ('Run', 'Walk', 'Cycle')),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserID) REFERENCES dbo.Users (UserID)
);
CREATE INDEX IX_Events_OrganiserID ON dbo.Events (OrganiserID);
GO

/* ============================================================
   4. Categories  (age or distance categories within an event)
   ============================================================ */
CREATE TABLE dbo.Categories (
    CategoryID      INT IDENTITY(1,1) NOT NULL,
    EventID         INT NOT NULL,
    CategoryName    NVARCHAR(100) NOT NULL,
    DistanceKm      DECIMAL(5,2) NULL,           -- [v2] now NULL for age-only categories
    MinAge          INT NULL,                    -- [v2] age categories, e.g. Senior = 20+
    MaxAge          INT NULL,                    -- [v2] age categories, e.g. Under 20 = max 19
    MaxParticipants INT NOT NULL DEFAULT 100,
    EntryFee        DECIMAL(8,2) NOT NULL DEFAULT 0.0,
    CONSTRAINT PK_Categories PRIMARY KEY (CategoryID),
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventID) REFERENCES dbo.Events (EventID) ON DELETE CASCADE  -- [v2]
);
CREATE INDEX IX_Categories_EventID ON dbo.Categories (EventID);
GO

/* ============================================================
   5. EventEnrolments  (Participant + Category, and through it the Event)
   ============================================================ */
CREATE TABLE dbo.EventEnrolments (
    EnrolmentID     INT IDENTITY(1,1) NOT NULL,
    ParticipantID   INT NOT NULL,
    CategoryID      INT NOT NULL,
    BibNumber       NVARCHAR(10) NULL,
    EnrolmentDate   DATETIME NOT NULL DEFAULT (GETDATE()),
    Status          NVARCHAR(20) NOT NULL DEFAULT N'Confirmed',
    CONSTRAINT PK_EventEnrolments PRIMARY KEY (EnrolmentID),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryID) REFERENCES dbo.Categories (CategoryID) ON DELETE CASCADE,  -- [v2]
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantID) REFERENCES dbo.Users (UserID)
);
CREATE UNIQUE INDEX UQ_Enrolment_Participant_Category ON dbo.EventEnrolments (ParticipantID, CategoryID);
CREATE INDEX IX_EventEnrolments_CategoryID ON dbo.EventEnrolments (CategoryID);
GO

/* ============================================================
   6. Results  (one-to-one with EventEnrolments)
   ============================================================ */
CREATE TABLE dbo.Results (
    ResultID        INT IDENTITY(1,1) NOT NULL,
    EnrolmentID     INT NOT NULL,
    FinishTime      TIME NULL,
    Position        INT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT N'Pending',
    CONSTRAINT PK_Results PRIMARY KEY (ResultID),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentID) REFERENCES dbo.EventEnrolments (EnrolmentID) ON DELETE CASCADE  -- [v2]
);
CREATE UNIQUE INDEX UQ_Results_Enrolment ON dbo.Results (EnrolmentID);
GO

/* ============================================================
   EF Core migration history
   Marks this schema as already migrated, so the API can run against a
   database created by this script without trying to create the tables again.
   ============================================================ */
CREATE TABLE dbo.__EFMigrationsHistory (
    MigrationId     NVARCHAR(150) NOT NULL,
    ProductVersion  NVARCHAR(32) NOT NULL,
    CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
);
INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
VALUES (N'20261012123717_InitialCreate', N'8.0.11');
GO

/* ============================================================
   SEED DATA
   ============================================================ */

-- Roles (fixed IDs used by the API)
SET IDENTITY_INSERT dbo.Roles ON;
INSERT INTO dbo.Roles (RoleID, RoleName) VALUES (1, 'Organiser'), (2, 'Participant'), (3, 'Admin');
SET IDENTITY_INSERT dbo.Roles OFF;
GO

-- Users: 2 Organisers, 2 Participants, 1 Admin.
-- [v2] Real ASP.NET Core Identity (PBKDF2-HMACSHA512) hashes, so these accounts can log in:
--      UserID 1-4 password: Password123!     Admin (UserID 5) password: Admin123!
INSERT INTO dbo.Users (RoleID, FullName, Email, PasswordHash, PhoneNumber) VALUES
(1, 'Thabo Mokoena',   'thabo.mokoena@raceday.co.za', 'AQAAAAIAAYagAAAAECIo//4Kk4JAfXUP65gyAEP/l2jdo2soBWmeMJSKH2OUyoxAoR+OzRNswOhVderQTA==', '0821234567'),
(1, 'Lindiwe Dube',    'lindiwe.dube@raceday.co.za',  'AQAAAAIAAYagAAAAECIo//4Kk4JAfXUP65gyAEP/l2jdo2soBWmeMJSKH2OUyoxAoR+OzRNswOhVderQTA==', '0827654321'),
(2, 'Sarah Botha',     'sarah.botha@example.com',     'AQAAAAIAAYagAAAAECIo//4Kk4JAfXUP65gyAEP/l2jdo2soBWmeMJSKH2OUyoxAoR+OzRNswOhVderQTA==', '0731112222'),
(2, 'Michael van Wyk', 'michael.vanwyk@example.com',  'AQAAAAIAAYagAAAAECIo//4Kk4JAfXUP65gyAEP/l2jdo2soBWmeMJSKH2OUyoxAoR+OzRNswOhVderQTA==', '0733334444'),
(3, 'RaceDay Admin',   'admin@raceday.co.za',         'AQAAAAIAAYagAAAAEGdPLji8e1eUEYhPO/OXb82MfPqgM4RmbSELDxovKRRJSXwLQrACzplQLgZQCY0sxA==', NULL);
GO

-- Events: one of each type, owned by the two organisers (UserID 1 and 2)
INSERT INTO dbo.Events (OrganiserID, EventName, EventDate, Location, Description, DistanceKm, EventType) VALUES
(1, 'Pretoria Spring Marathon', '2026-11-15', 'Pretoria, Gauteng',       'Annual road race through the Union Buildings precinct.', 42.20, 'Run'),
(1, 'Centurion Night Walk',     '2026-12-05', 'Centurion, Gauteng',      'Evening fun walk with a glow-in-the-dark theme.',          5.00, 'Walk'),
(2, 'Cape Cycle Challenge',     '2026-09-26', 'Cape Town, Western Cape', 'Road cycle tour around the Cape Peninsula.',               60.00, 'Cycle');
GO

-- Categories: distance categories and age categories
INSERT INTO dbo.Categories (EventID, CategoryName, DistanceKm, MinAge, MaxAge, MaxParticipants, EntryFee) VALUES
(1, '10km Fun Run',  10.00, NULL, NULL, 500, 150.00),
(1, '42km Marathon', 42.20, NULL, NULL, 300, 350.00),
(2, 'Under 20',       NULL, NULL,   19, 200,  50.00),
(2, 'Senior',         NULL,   20, NULL, 400, 100.00),
(3, '30km Sprint',   30.00, NULL, NULL, 200, 250.00),
(3, '60km Tour',     60.00, NULL, NULL, 150, 400.00);
GO

-- EventEnrolments: the two participants (UserID 3 and 4)
INSERT INTO dbo.EventEnrolments (ParticipantID, CategoryID, BibNumber, Status) VALUES
(3, 1, 'RD00001', 'Confirmed'),
(3, 5, 'RD00002', 'Confirmed'),
(4, 4, 'RD00003', 'Confirmed'),
(4, 5, 'RD00004', 'Confirmed');
GO

-- Results: the completed Cape Cycle Challenge (30km Sprint)
INSERT INTO dbo.Results (EnrolmentID, FinishTime, Position, Status) VALUES
(2, '00:58:41', 2, 'Final'),
(4, '00:55:03', 1, 'Final');
GO
