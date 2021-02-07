-- create schema segment_challenge collate utf8mb4_general_ci;

-- use segment_challenge;

create table ActivityUpdate
(
    ChallengeId int not null,
    ActivityId bigint not null,
    AthleteId bigint not null,
    UpdateId int not null,
    UpdatedAt datetime not null,
    primary key (ChallengeId, ActivityId)
);

create index ActivityUpdate_AthleteId_index
    on ActivityUpdate (AthleteId);

create table AgeGroup
(
    ChallengeId int not null,
    MaximumAge int not null,
    Description text not null,
    primary key (ChallengeId, MaximumAge)
);

create table Athlete
(
    Id bigint not null
        primary key,
    Username text null,
    FirstName text null,
    LastName text null,
    Gender char not null,
    BirthDate date null,
    Email varchar(256) null,
    ProfilePicture text null,
    AccessToken text null,
    RefreshToken text null,
    TokenExpiration datetime null,
    PasswordHash varchar(100) null,
    constraint Athlete_Email_uindex
        unique (Email)
);

create index Athlete__BirthDate
    on Athlete (BirthDate);

create table Challenge
(
    Id int auto_increment
        primary key,
    Name varchar(50) not null,
    DisplayName text null,
    Description text null,
    SegmentId bigint not null,
    StartDate datetime not null,
    EndDate datetime null,
    ChallengeType smallint default 0 not null,
    UseMovingTime bit default b'0' not null,
    GpxData mediumtext null,
    InviteCode varchar(50) null,
    RegistrationLink text null,
    constraint Challenge_InviteCode_uindex
        unique (InviteCode),
    constraint Challenge_Name_uindex
        unique (Name)
);

create table ChallengeRegistration
(
    ChallengeId int not null,
    AthleteId bigint not null,
    primary key (ChallengeId, AthleteId)
);

create table Effort
(
    Id bigint not null
        primary key,
    AthleteId bigint not null,
    ActivityId bigint not null,
    SegmentId bigint not null,
    ElapsedTime int not null,
    StartDate datetime not null
);

create table `Update`
(
    Id int auto_increment
        primary key,
    AthleteCount int default 0 not null,
    ActivityCount int default 0 not null,
    SkippedActivityCount int default 0 not null,
    EffortCount int default 0 not null,
    ErrorCount int default 0 not null,
    Progress float default 0 not null,
    StartTime datetime null,
    EndTime datetime null,
    AthleteId bigint null,
    ChallengeId int not null
);
