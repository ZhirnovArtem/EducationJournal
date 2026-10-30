CREATE TABLE Roles (
    RoleId   SERIAL PRIMARY KEY,
    Code     VARCHAR(30) NOT NULL UNIQUE,
    Title    VARCHAR(50) NOT NULL
);

INSERT INTO Roles (Code, Title) VALUES
('Admin','Администратор'),
('Teacher','Преподаватель'),
('Student','Студент');

CREATE TABLE Users (
    UserId         SERIAL PRIMARY KEY,
    Login          VARCHAR(100) NOT NULL UNIQUE,
    Email          VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash   BYTEA NOT NULL,
    PasswordAlgo   VARCHAR(20) NOT NULL DEFAULT 'argon2id',
    RoleId         INT NOT NULL REFERENCES Roles(RoleId),
    IsActive       BOOLEAN NOT NULL DEFAULT TRUE,
    FailedAttempts INT NOT NULL DEFAULT 0,
    LockedUntil    TIMESTAMP NULL,
    LastLoginAt    TIMESTAMP NULL,
    CreatedAt      TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt      TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt      TIMESTAMP NULL
);

CREATE TABLE RefreshTokens (
    RefreshTokenId BIGSERIAL PRIMARY KEY,
    UserId         INT NOT NULL REFERENCES Users(UserId) ON DELETE CASCADE,
    TokenHash      CHAR(64) NOT NULL UNIQUE,
    IssuedAt       TIMESTAMP NOT NULL DEFAULT NOW(),
    ExpiresAt      TIMESTAMP NOT NULL,
    RevokedAt      TIMESTAMP NULL,
    UserAgent      VARCHAR(255)
);

CREATE TABLE LoginAttempts (
    LoginAttemptId BIGSERIAL PRIMARY KEY,
    Login          VARCHAR(100) NOT NULL,
    Success        BOOLEAN NOT NULL,
    IpAddress      VARCHAR(45),
    UserAgent      VARCHAR(255),
    AttemptedAt    TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE PasswordResets (
    PasswordResetId BIGSERIAL PRIMARY KEY,
    UserId          INT NOT NULL REFERENCES Users(UserId) ON DELETE CASCADE,
    TokenHash       CHAR(64) NOT NULL,
    ExpiresAt       TIMESTAMP NOT NULL,
    UsedAt          TIMESTAMP NULL,
    CreatedAt       TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE Groups (
    GroupId   SERIAL PRIMARY KEY,
    Title     VARCHAR(50) NOT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt TIMESTAMP NULL
);

CREATE UNIQUE INDEX groups_title_active_key
    ON Groups (Title)
    WHERE DeletedAt IS NULL;

CREATE TABLE Disciplines (
    DisciplineId SERIAL PRIMARY KEY,
    Title        VARCHAR(100) NOT NULL UNIQUE,
    DeletedAt    TIMESTAMP NULL
);

CREATE UNIQUE INDEX disciplines_title_active_key
    ON Disciplines (Title)
    WHERE DeletedAt IS NULL;

CREATE TABLE Grades (
    GradeId    SERIAL PRIMARY KEY,
    GradeValue SMALLINT NOT NULL UNIQUE CHECK (GradeValue BETWEEN 1 AND 5)
);
INSERT INTO Grades (GradeValue) VALUES (1),(2),(3),(4),(5);

CREATE TABLE Attendance (
    AttendanceId SERIAL PRIMARY KEY,
    Title        VARCHAR(30) NOT NULL UNIQUE
);
INSERT INTO Attendance (Title) VALUES
('Присутствовал'),('Опоздал'),('Отсутствовал');

CREATE TABLE LessonTimes (
    LessonTimeId SERIAL PRIMARY KEY,
    PairNumber   SMALLINT NOT NULL UNIQUE,
    StartTime    TIME NOT NULL,
    EndTime      TIME NOT NULL,
    BreakMinutes INT NULL,
    IsActive     BOOLEAN NOT NULL DEFAULT TRUE,
    CHECK (EndTime > StartTime)
);
INSERT INTO LessonTimes (PairNumber, StartTime, EndTime, BreakMinutes) VALUES
(1,'09:00','10:30',10),
(2,'10:40','12:10',10),
(3,'12:20','13:50',40),
(4,'14:30','16:00',10),
(5,'16:10','17:40',10),
(6,'17:50','19:20',NULL);

CREATE TABLE CoinTypes (
    CoinTypeId    SERIAL PRIMARY KEY,
    Code          VARCHAR(30) NOT NULL UNIQUE,
    Title         VARCHAR(100) NOT NULL,
    DefaultAmount INT NOT NULL DEFAULT 1,
    Icon          VARCHAR(50),
    IsActive      BOOLEAN NOT NULL DEFAULT TRUE
);
INSERT INTO CoinTypes (Code, Title, DefaultAmount, Icon) VALUES
('activity','Активность',5,'⚡'),
('attendance','Посещаемость',2,'✅'),
('penalty','Штраф',-3,'⚠️'),
('bonus','Бонус',15,'🎁');

CREATE TABLE Holidays (
    HolidayId   SERIAL PRIMARY KEY,
    Date        DATE NOT NULL,
    Title       VARCHAR(100) NOT NULL,
    IsStudyDay  BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE Students (
    StudentId   SERIAL PRIMARY KEY,
    UserId      INT NOT NULL UNIQUE REFERENCES Users(UserId) ON DELETE CASCADE,
    FirstName   VARCHAR(50) NOT NULL,
    LastName    VARCHAR(50) NOT NULL,
    Patronymic  VARCHAR(50),
    BirthDate   DATE NOT NULL,
    PhoneNumber VARCHAR(20) UNIQUE,
    Email       VARCHAR(100) NOT NULL UNIQUE,
    GroupId     INT REFERENCES Groups(GroupId) ON DELETE SET NULL,
    PhotoFileId BIGINT NULL,
    CreatedAt   TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt   TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt   TIMESTAMP NULL
);

CREATE TABLE Teachers (
    TeacherId   SERIAL PRIMARY KEY,
    UserId      INT NOT NULL UNIQUE REFERENCES Users(UserId) ON DELETE CASCADE,
    FirstName   VARCHAR(50) NOT NULL,
    LastName    VARCHAR(50) NOT NULL,
    Patronymic  VARCHAR(50),
    PhoneNumber VARCHAR(20) UNIQUE,
    Email       VARCHAR(100) NOT NULL UNIQUE,
    PhotoFileId BIGINT NULL,
    CreatedAt   TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt   TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt   TIMESTAMP NULL
);

CREATE TABLE Admins (
    AdminId     SERIAL PRIMARY KEY,
    UserId      INT NOT NULL UNIQUE REFERENCES Users(UserId) ON DELETE CASCADE,
    FirstName   VARCHAR(50) NOT NULL,
    LastName    VARCHAR(50) NOT NULL,
    Patronymic  VARCHAR(50),
    Email       VARCHAR(100) NOT NULL UNIQUE,
    PhotoFileId BIGINT NULL,
    CreatedAt   TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt   TIMESTAMP NULL
);

CREATE TABLE TeacherDisciplines (
    TeacherId    INT NOT NULL REFERENCES Teachers(TeacherId) ON DELETE CASCADE,
    DisciplineId INT NOT NULL REFERENCES Disciplines(DisciplineId) ON DELETE CASCADE,
    PRIMARY KEY (TeacherId, DisciplineId)
);

CREATE TABLE Files (
    FileId       BIGSERIAL PRIMARY KEY,
    Bucket       VARCHAR(100) NOT NULL,
    ObjectKey    VARCHAR(512) NOT NULL,
    OriginalName VARCHAR(255) NOT NULL,
    MimeType     VARCHAR(127) NOT NULL,
    SizeBytes    BIGINT NOT NULL,
    Checksum     CHAR(64) NOT NULL,
    UploadedBy   INT NOT NULL REFERENCES Users(UserId),
    UploadedAt   TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt    TIMESTAMP NULL,
    UNIQUE (Bucket, ObjectKey)
);

CREATE TABLE HomeworkArchives (
    ArchiveId         BIGSERIAL PRIMARY KEY,
    FileId            BIGINT NOT NULL REFERENCES Files(FileId) ON DELETE CASCADE,
    OriginalName      VARCHAR(255) NOT NULL,
    FileCount         INT NOT NULL DEFAULT 0,
    UncompressedBytes BIGINT NOT NULL DEFAULT 0,
    CompressedBytes   BIGINT NOT NULL DEFAULT 0,
    CompressionRatio  NUMERIC(5,2) NULL,
    Algorithm         VARCHAR(30) NOT NULL DEFAULT 'zip-deflate',
    IsEncrypted       BOOLEAN NOT NULL DEFAULT FALSE,
    PasswordHash      BYTEA NULL,
    Checksum          CHAR(64) NOT NULL,
    CreatedAt         TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt         TIMESTAMP NULL
);

CREATE TABLE HomeworkArchiveFiles (
    ArchiveFileId  BIGSERIAL PRIMARY KEY,
    ArchiveId      BIGINT NOT NULL REFERENCES HomeworkArchives(ArchiveId) ON DELETE CASCADE,
    PathInArchive  VARCHAR(512) NOT NULL,
    MimeType       VARCHAR(127),
    SizeBytes      BIGINT NOT NULL,
    CompressedSize BIGINT NULL,
    Checksum       CHAR(64) NULL,
    CreatedAt      TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE (ArchiveId, PathInArchive)
);

CREATE TABLE ScheduleTemplates (
    TemplateId   SERIAL PRIMARY KEY,
    Title        VARCHAR(100) NOT NULL,
    DisciplineId INT NOT NULL REFERENCES Disciplines(DisciplineId),
    TeacherId    INT NOT NULL REFERENCES Teachers(TeacherId),
    GroupId      INT NOT NULL REFERENCES Groups(GroupId) ON DELETE CASCADE,
    LessonTimeId INT NOT NULL REFERENCES LessonTimes(LessonTimeId),
    DayOfWeek    SMALLINT NOT NULL CHECK (DayOfWeek BETWEEN 1 AND 7),
    Room         VARCHAR(20),
    LessonType   VARCHAR(20) NOT NULL DEFAULT 'Lecture',
    ValidFrom    DATE NOT NULL,
    ValidTo      DATE NULL,
    CreatedAt    TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt    TIMESTAMP NULL
);

CREATE TABLE MonthShedule (
    MonthScheduleId SERIAL PRIMARY KEY,
    Title           VARCHAR(100) NOT NULL,
    DisciplineId    INT NOT NULL REFERENCES Disciplines(DisciplineId),
    TeacherId       INT NOT NULL REFERENCES Teachers(TeacherId),
    GroupId         INT NOT NULL REFERENCES Groups(GroupId) ON DELETE CASCADE,
    LessonTimeId    INT NOT NULL REFERENCES LessonTimes(LessonTimeId),
    Date            DATE NOT NULL,
    Room            VARCHAR(20),
    LessonType      VARCHAR(20) NOT NULL DEFAULT 'Lecture',
    CreatedAt       TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt       TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt       TIMESTAMP NULL,
    UNIQUE (GroupId, Date, LessonTimeId),
    UNIQUE (TeacherId, Date, LessonTimeId)
);

CREATE TABLE Schedule (
    ScheduleId       SERIAL PRIMARY KEY,
    MonthScheduleId  INT NOT NULL UNIQUE REFERENCES MonthShedule(MonthScheduleId) ON DELETE CASCADE,
    ActualTeacherId  INT NULL REFERENCES Teachers(TeacherId) ON DELETE SET NULL,
    Status           VARCHAR(20) NOT NULL DEFAULT 'Planned',
    Comment          VARCHAR(255),
    ConductedAt      TIMESTAMP NULL,
    CreatedAt        TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt        TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt        TIMESTAMP NULL
);

CREATE TABLE ScheduleAttendance (
    ScheduleId   INT NOT NULL REFERENCES Schedule(ScheduleId) ON DELETE CASCADE,
    StudentId    INT NOT NULL REFERENCES Students(StudentId) ON DELETE CASCADE,
    AttendanceId INT NOT NULL REFERENCES Attendance(AttendanceId),
    Comment      VARCHAR(255),
    MarkedBy     INT NULL REFERENCES Users(UserId) ON DELETE SET NULL,
    MarkedAt     TIMESTAMP NULL,
    PRIMARY KEY (ScheduleId, StudentId)
);

CREATE TABLE Homeworks (
    HomeworkId       SERIAL PRIMARY KEY,
    StudentId        INT NOT NULL REFERENCES Students(StudentId) ON DELETE CASCADE,
    TeacherId        INT NOT NULL REFERENCES Teachers(TeacherId),
    DisciplineId     INT NOT NULL REFERENCES Disciplines(DisciplineId),
    Task             TEXT NOT NULL,
    TaskFileId       BIGINT NULL REFERENCES Files(FileId) ON DELETE SET NULL,
    HomeworkText     TEXT,
    AnswerFileId     BIGINT NULL REFERENCES Files(FileId) ON DELETE SET NULL,
    AnswerArchiveId  BIGINT NULL REFERENCES HomeworkArchives(ArchiveId) ON DELETE SET NULL,
    DateOfIssue      DATE NOT NULL,
    DeadLine         DATE NOT NULL,
    DueDate          DATE NULL,
    GradeId          INT NULL REFERENCES Grades(GradeId) ON DELETE SET NULL,
    CreatedAt        TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt        TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt        TIMESTAMP NULL,
    CHECK (DeadLine >= DateOfIssue)
);
CREATE INDEX ix_homeworks_student ON Homeworks(StudentId);
CREATE INDEX ix_homeworks_teacher ON Homeworks(TeacherId);
CREATE INDEX ix_homeworks_deadline ON Homeworks(DeadLine);

CREATE TABLE LessonGrades (
    LessonGradeId BIGSERIAL PRIMARY KEY,
    ScheduleId    INT NOT NULL REFERENCES Schedule(ScheduleId) ON DELETE CASCADE,
    StudentId     INT NOT NULL REFERENCES Students(StudentId) ON DELETE CASCADE,
    GradeId       INT NOT NULL REFERENCES Grades(GradeId),
    Comment       VARCHAR(255),
    GradedBy      INT NOT NULL REFERENCES Users(UserId),
    GradedAt      TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt     TIMESTAMP NOT NULL DEFAULT NOW(),
    DeletedAt     TIMESTAMP NULL
);

CREATE TABLE Coins (
    CoinId     BIGSERIAL PRIMARY KEY,
    StudentId  INT NOT NULL REFERENCES Students(StudentId) ON DELETE CASCADE,
    CoinTypeId INT NOT NULL REFERENCES CoinTypes(CoinTypeId),
    Amount     INT NOT NULL,
    Reason     VARCHAR(255),
    ScheduleId INT NULL REFERENCES Schedule(ScheduleId) ON DELETE SET NULL,
    HomeworkId INT NULL REFERENCES Homeworks(HomeworkId) ON DELETE SET NULL,
    AwardedBy  INT NOT NULL REFERENCES Users(UserId),
    AwardedAt  TIMESTAMP NOT NULL DEFAULT NOW(),
    RevokedAt  TIMESTAMP NULL,
    RevokedBy  INT NULL REFERENCES Users(UserId) ON DELETE SET NULL
);

CREATE TABLE Notifications (
    NotificationId BIGSERIAL PRIMARY KEY,
    UserId         INT NOT NULL REFERENCES Users(UserId) ON DELETE CASCADE,
    Title          VARCHAR(255) NOT NULL,
    Body           TEXT,
    Url            VARCHAR(255),
    IsRead         BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt      TIMESTAMP NOT NULL DEFAULT NOW(),
    ReadAt         TIMESTAMP NULL
);

CREATE TABLE AuditLog (
    AuditLogId BIGSERIAL PRIMARY KEY,
    UserId     INT NULL REFERENCES Users(UserId) ON DELETE SET NULL,
    TableName  VARCHAR(64) NOT NULL,
    RecordId   BIGINT NOT NULL,
    Action     VARCHAR(10) NOT NULL,
    OldValues  TEXT,
    NewValues  TEXT,
    ChangedAt  TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_users_email ON Users(Email);
CREATE INDEX ix_users_login ON Users(Login);
CREATE INDEX ix_files_uploader ON Files(UploadedBy);
CREATE INDEX ix_schedule_date ON MonthShedule(Date);
CREATE INDEX ix_coins_student ON Coins(StudentId);