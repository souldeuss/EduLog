IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Class] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Class] PRIMARY KEY ([Id])
);

CREATE TABLE [Student] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Surname] nvarchar(max) NOT NULL,
    [Patronymic] nvarchar(max) NOT NULL,
    [SchoolClassId] int NOT NULL,
    [ClassId] int NOT NULL,
    CONSTRAINT [PK_Student] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Student_Class_ClassId] FOREIGN KEY ([ClassId]) REFERENCES [Class] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Teacher] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Surname] nvarchar(max) NOT NULL,
    [Patronymic] nvarchar(max) NOT NULL,
    [ClassId] int NULL,
    CONSTRAINT [PK_Teacher] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Teacher_Class_ClassId] FOREIGN KEY ([ClassId]) REFERENCES [Class] ([Id])
);

CREATE TABLE [Absence] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [SubjectId] int NOT NULL,
    [Date] datetime2 NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Absence] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Absence_Student_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Grade] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [SubjectId] int NOT NULL,
    [Date] datetime2 NOT NULL,
    [Value] int NOT NULL,
    CONSTRAINT [PK_Grade] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Grade_Student_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Subject] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [TeacherId] int NOT NULL,
    CONSTRAINT [PK_Subject] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Subject_Teacher_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teacher] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Absence_StudentId] ON [Absence] ([StudentId]);

CREATE INDEX [IX_Grade_StudentId] ON [Grade] ([StudentId]);

CREATE INDEX [IX_Student_ClassId] ON [Student] ([ClassId]);

CREATE INDEX [IX_Subject_TeacherId] ON [Subject] ([TeacherId]);

CREATE INDEX [IX_Teacher_ClassId] ON [Teacher] ([ClassId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250614194908_InitialCreate', N'9.0.6');

DECLARE @var sysname;
SELECT @var = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Student]') AND [c].[name] = N'SchoolClassId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Student] DROP CONSTRAINT [' + @var + '];');
ALTER TABLE [Student] DROP COLUMN [SchoolClassId];

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Class]'))
    SET IDENTITY_INSERT [Class] ON;
INSERT INTO [Class] ([Id], [Name])
VALUES (1, N'5A'),
(2, N'5B');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Class]'))
    SET IDENTITY_INSERT [Class] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClassId', N'Name', N'Patronymic', N'Surname') AND [object_id] = OBJECT_ID(N'[Teacher]'))
    SET IDENTITY_INSERT [Teacher] ON;
INSERT INTO [Teacher] ([Id], [ClassId], [Name], [Patronymic], [Surname])
VALUES (1, NULL, N'Олена', N'Петрівна', N'Іваненко');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClassId', N'Name', N'Patronymic', N'Surname') AND [object_id] = OBJECT_ID(N'[Teacher]'))
    SET IDENTITY_INSERT [Teacher] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClassId', N'Name', N'Patronymic', N'Surname') AND [object_id] = OBJECT_ID(N'[Student]'))
    SET IDENTITY_INSERT [Student] ON;
INSERT INTO [Student] ([Id], [ClassId], [Name], [Patronymic], [Surname])
VALUES (1, 1, N'Ім''я1', N'По-батькові1', N'Прізвище1'),
(2, 1, N'Ім''я2', N'По-батькові2', N'Прізвище2'),
(3, 1, N'Ім''я3', N'По-батькові3', N'Прізвище3'),
(4, 1, N'Ім''я4', N'По-батькові4', N'Прізвище4'),
(5, 1, N'Ім''я5', N'По-батькові5', N'Прізвище5'),
(6, 1, N'Ім''я6', N'По-батькові6', N'Прізвище6'),
(7, 1, N'Ім''я7', N'По-батькові7', N'Прізвище7'),
(8, 1, N'Ім''я8', N'По-батькові8', N'Прізвище8'),
(9, 1, N'Ім''я9', N'По-батькові9', N'Прізвище9'),
(10, 1, N'Ім''я10', N'По-батькові10', N'Прізвище10'),
(11, 1, N'Ім''я11', N'По-батькові11', N'Прізвище11'),
(12, 1, N'Ім''я12', N'По-батькові12', N'Прізвище12'),
(13, 1, N'Ім''я13', N'По-батькові13', N'Прізвище13'),
(14, 1, N'Ім''я14', N'По-батькові14', N'Прізвище14'),
(15, 1, N'Ім''я15', N'По-батькові15', N'Прізвище15'),
(16, 1, N'Ім''я16', N'По-батькові16', N'Прізвище16'),
(17, 1, N'Ім''я17', N'По-батькові17', N'Прізвище17'),
(18, 1, N'Ім''я18', N'По-батькові18', N'Прізвище18'),
(19, 1, N'Ім''я19', N'По-батькові19', N'Прізвище19'),
(20, 1, N'Ім''я20', N'По-батькові20', N'Прізвище20'),
(21, 2, N'Ім''я21', N'По-батькові21', N'Прізвище21'),
(22, 2, N'Ім''я22', N'По-батькові22', N'Прізвище22'),
(23, 2, N'Ім''я23', N'По-батькові23', N'Прізвище23'),
(24, 2, N'Ім''я24', N'По-батькові24', N'Прізвище24'),
(25, 2, N'Ім''я25', N'По-батькові25', N'Прізвище25'),
(26, 2, N'Ім''я26', N'По-батькові26', N'Прізвище26'),
(27, 2, N'Ім''я27', N'По-батькові27', N'Прізвище27'),
(28, 2, N'Ім''я28', N'По-батькові28', N'Прізвище28'),
(29, 2, N'Ім''я29', N'По-батькові29', N'Прізвище29'),
(30, 2, N'Ім''я30', N'По-батькові30', N'Прізвище30'),
(31, 2, N'Ім''я31', N'По-батькові31', N'Прізвище31'),
(32, 2, N'Ім''я32', N'По-батькові32', N'Прізвище32'),
(33, 2, N'Ім''я33', N'По-батькові33', N'Прізвище33'),
(34, 2, N'Ім''я34', N'По-батькові34', N'Прізвище34'),
(35, 2, N'Ім''я35', N'По-батькові35', N'Прізвище35'),
(36, 2, N'Ім''я36', N'По-батькові36', N'Прізвище36'),
(37, 2, N'Ім''я37', N'По-батькові37', N'Прізвище37'),
(38, 2, N'Ім''я38', N'По-батькові38', N'Прізвище38'),
(39, 2, N'Ім''я39', N'По-батькові39', N'Прізвище39'),
(40, 2, N'Ім''я40', N'По-батькові40', N'Прізвище40');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClassId', N'Name', N'Patronymic', N'Surname') AND [object_id] = OBJECT_ID(N'[Student]'))
    SET IDENTITY_INSERT [Student] OFF;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250615131546_SeedInitialData', N'9.0.6');

ALTER TABLE [Teacher] DROP CONSTRAINT [FK_Teacher_Class_ClassId];

DROP INDEX [IX_Teacher_ClassId] ON [Teacher];

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Teacher]') AND [c].[name] = N'ClassId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Teacher] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Teacher] DROP COLUMN [ClassId];

ALTER TABLE [Teacher] ADD [PhotoPath] nvarchar(max) NOT NULL DEFAULT N'';

CREATE TABLE [ClassTeacher] (
    [ClassesId] int NOT NULL,
    [TeachersId] int NOT NULL,
    CONSTRAINT [PK_ClassTeacher] PRIMARY KEY ([ClassesId], [TeachersId]),
    CONSTRAINT [FK_ClassTeacher_Class_ClassesId] FOREIGN KEY ([ClassesId]) REFERENCES [Class] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ClassTeacher_Teacher_TeachersId] FOREIGN KEY ([TeachersId]) REFERENCES [Teacher] ([Id]) ON DELETE CASCADE
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'TeacherId') AND [object_id] = OBJECT_ID(N'[Subject]'))
    SET IDENTITY_INSERT [Subject] ON;
INSERT INTO [Subject] ([Id], [Name], [TeacherId])
VALUES (1, N'Англійська мова', 1);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'TeacherId') AND [object_id] = OBJECT_ID(N'[Subject]'))
    SET IDENTITY_INSERT [Subject] OFF;

UPDATE [Teacher] SET [PhotoPath] = N'D:\UN\Практика\EduLog\EduLog\Data\UserImages\User-avatar.svg.png'
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


CREATE INDEX [IX_ClassTeacher_TeachersId] ON [ClassTeacher] ([TeachersId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250615160919_AddSubjectToTable', N'9.0.6');

DROP TABLE [ClassTeacher];

ALTER TABLE [Class] ADD [TeacherId] int NULL;

UPDATE [Class] SET [TeacherId] = 1
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


UPDATE [Class] SET [TeacherId] = NULL
WHERE [Id] = 2;
SELECT @@ROWCOUNT;


UPDATE [Teacher] SET [PhotoPath] = N'~/Data/UserImages/User-avatar.svg.png'
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250616164305_UpdateClasses', N'9.0.6');

ALTER TABLE [Subject] ADD [ClassId] int NOT NULL DEFAULT 0;

UPDATE [Subject] SET [ClassId] = 1
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250617190748_FixSubjectDependence', N'9.0.6');

CREATE TABLE [ClassSubject] (
    [ClassId] int NOT NULL,
    [SubjectId] int NOT NULL,
    CONSTRAINT [PK_ClassSubject] PRIMARY KEY ([ClassId], [SubjectId]),
    CONSTRAINT [FK_ClassSubject_Class_ClassId] FOREIGN KEY ([ClassId]) REFERENCES [Class] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ClassSubject_Subject_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subject] ([Id]) ON DELETE CASCADE
);

UPDATE [Class] SET [Name] = N'5-A'
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


UPDATE [Class] SET [Name] = N'5-B'
WHERE [Id] = 2;
SELECT @@ROWCOUNT;


CREATE INDEX [IX_ClassSubject_SubjectId] ON [ClassSubject] ([SubjectId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250620154917_SubjectClass', N'9.0.6');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250620155348_SubjectClassFix', N'9.0.6');

CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [TeacherId] int NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUsers_Teacher_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teacher] ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE INDEX [IX_AspNetUsers_TeacherId] ON [AspNetUsers] ([TeacherId]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260310204333_AddIdentity', N'9.0.6');

DELETE FROM [Student]
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 2;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 3;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 4;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 5;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 6;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 7;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 8;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 9;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 10;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 11;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 12;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 13;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 14;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 15;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 16;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 17;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 18;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 19;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 20;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 21;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 22;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 23;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 24;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 25;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 26;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 27;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 28;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 29;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 30;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 31;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 32;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 33;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 34;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 35;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 36;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 37;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 38;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 39;
SELECT @@ROWCOUNT;


DELETE FROM [Student]
WHERE [Id] = 40;
SELECT @@ROWCOUNT;


DELETE FROM [Subject]
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


DELETE FROM [Class]
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


DELETE FROM [Class]
WHERE [Id] = 2;
SELECT @@ROWCOUNT;


DELETE FROM [Teacher]
WHERE [Id] = 1;
SELECT @@ROWCOUNT;


ALTER TABLE [Teacher] ADD [SchoolId] int NOT NULL DEFAULT 0;

ALTER TABLE [Subject] ADD [SchoolId] int NOT NULL DEFAULT 0;

ALTER TABLE [Student] ADD [SchoolId] int NOT NULL DEFAULT 0;

ALTER TABLE [Grade] ADD [SchoolId] int NOT NULL DEFAULT 0;

ALTER TABLE [ClassSubject] ADD [SchoolId] int NOT NULL DEFAULT 0;

ALTER TABLE [Class] ADD [SchoolId] int NOT NULL DEFAULT 0;

ALTER TABLE [AspNetUsers] ADD [SchoolId] int NULL;

ALTER TABLE [Absence] ADD [SchoolId] int NOT NULL DEFAULT 0;

CREATE TABLE [School] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Address] nvarchar(max) NULL,
    [Type] nvarchar(max) NULL,
    CONSTRAINT [PK_School] PRIMARY KEY ([Id])
);

CREATE TABLE [Invitation] (
    [Id] int NOT NULL IDENTITY,
    [SchoolId] int NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Token] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsUsed] bit NOT NULL,
    CONSTRAINT [PK_Invitation] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Invitation_School_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [School] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetUsers_SchoolId] ON [AspNetUsers] ([SchoolId]);

CREATE INDEX [IX_Invitation_SchoolId] ON [Invitation] ([SchoolId]);

ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_School_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [School] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260310215028_MultiTenantRolesInvitations', N'9.0.6');

CREATE TABLE [AcademicYear] (
    [Id] int NOT NULL IDENTITY,
    [SchoolId] int NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [IsCurrent] bit NOT NULL,
    [IsArchived] bit NOT NULL,
    CONSTRAINT [PK_AcademicYear] PRIMARY KEY ([Id])
);

CREATE TABLE [ScheduleSlot] (
    [Id] int NOT NULL IDENTITY,
    [SchoolId] int NOT NULL,
    [AcademicYearId] int NOT NULL,
    [DayOfWeek] int NOT NULL,
    [LessonNumber] int NOT NULL,
    [ClassId] int NOT NULL,
    [SubjectId] int NOT NULL,
    [TeacherId] int NOT NULL,
    [Room] nvarchar(20) NULL,
    CONSTRAINT [PK_ScheduleSlot] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ScheduleSlot_AcademicYear_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYear] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ScheduleSlot_Class_ClassId] FOREIGN KEY ([ClassId]) REFERENCES [Class] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ScheduleSlot_Subject_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subject] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ScheduleSlot_Teacher_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teacher] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ScheduleSlot_AcademicYearId] ON [ScheduleSlot] ([AcademicYearId]);

CREATE INDEX [IX_ScheduleSlot_ClassId] ON [ScheduleSlot] ([ClassId]);

CREATE INDEX [IX_ScheduleSlot_SubjectId] ON [ScheduleSlot] ([SubjectId]);

CREATE INDEX [IX_ScheduleSlot_TeacherId] ON [ScheduleSlot] ([TeacherId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260310225211_AddScheduleAndAcademicYear', N'9.0.6');

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subject]') AND [c].[name] = N'ClassId');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Subject] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Subject] ALTER COLUMN [ClassId] int NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260311010318_SubjectClassIdNullable', N'9.0.6');

CREATE TABLE [SchoolEvent] (
    [Id] int NOT NULL IDENTITY,
    [SchoolId] int NOT NULL,
    [Date] datetime2 NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Color] nvarchar(max) NULL,
    CONSTRAINT [PK_SchoolEvent] PRIMARY KEY ([Id])
);

ALTER TABLE [Class] ADD [RoomId] int NULL;

CREATE TABLE [ClassTemplate] (
    [Id] int NOT NULL IDENTITY,
    [SchoolId] int NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ClassTemplate] PRIMARY KEY ([Id])
);

CREATE TABLE [Room] (
    [Id] int NOT NULL IDENTITY,
    [SchoolId] int NOT NULL,
    [Number] nvarchar(50) NOT NULL,
    [Capacity] int NULL,
    CONSTRAINT [PK_Room] PRIMARY KEY ([Id])
);

CREATE TABLE [TemplateSubject] (
    [TemplateId] int NOT NULL,
    [SubjectId] int NOT NULL,
    CONSTRAINT [PK_TemplateSubject] PRIMARY KEY ([TemplateId], [SubjectId]),
    CONSTRAINT [FK_TemplateSubject_ClassTemplate_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [ClassTemplate] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TemplateSubject_Subject_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subject] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Class_RoomId] ON [Class] ([RoomId]);

CREATE INDEX [IX_TemplateSubject_SubjectId] ON [TemplateSubject] ([SubjectId]);

ALTER TABLE [Class] ADD CONSTRAINT [FK_Class_Room_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Room] ([Id]) ON DELETE SET NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260415142150_AddSchoolEventsClassTemplatesAndRooms', N'9.0.6');

ALTER TABLE [Subject] ADD [DefaultRoomId] int NULL;

ALTER TABLE [Subject] ADD [HoursPerWeek] int NOT NULL DEFAULT 1;

CREATE TABLE [SubjectTeacher] (
    [SubjectId] int NOT NULL,
    [TeacherId] int NOT NULL,
    [SchoolId] int NOT NULL,
    CONSTRAINT [PK_SubjectTeacher] PRIMARY KEY ([SubjectId], [TeacherId]),
    CONSTRAINT [FK_SubjectTeacher_Subject_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subject] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubjectTeacher_Teacher_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teacher] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Subject_DefaultRoomId] ON [Subject] ([DefaultRoomId]);

CREATE INDEX [IX_SubjectTeacher_TeacherId] ON [SubjectTeacher] ([TeacherId]);

ALTER TABLE [Subject] ADD CONSTRAINT [FK_Subject_Room_DefaultRoomId] FOREIGN KEY ([DefaultRoomId]) REFERENCES [Room] ([Id]) ON DELETE SET NULL;


INSERT INTO SubjectTeacher (SubjectId, TeacherId, SchoolId)
SELECT s.Id, s.TeacherId, s.SchoolId
FROM Subject s
WHERE NOT EXISTS (
    SELECT 1
    FROM SubjectTeacher st
    WHERE st.SubjectId = s.Id AND st.TeacherId = s.TeacherId
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260415145149_AddSubjectTeachersHoursAndDefaultRoom', N'9.0.6');

COMMIT;
GO

