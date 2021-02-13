alter table Challenge add InviteCode varchar(50) null;
alter table Challenge add RegistrationLink text null;
alter table Challenge add RouteMapImage mediumblob null;

create unique index Challenge_InviteCode_uindex on Challenge (InviteCode);

alter table Athlete modify Email varchar(256) null;

create unique index Athlete_Email_uindex on Athlete (Email);

alter table Athlete add PasswordHash varchar(100) null;
