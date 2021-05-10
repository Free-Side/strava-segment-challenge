alter table ChallengeRegistration add SpecialCategoryId int null;

create table SpecialCategory
(
    ChallengeId int not null,
    SpecialCategoryId int not null,
    CategoryName varchar(100) not null,
    Message text null,
    primary key (ChallengeId, SpecialCategoryId)
);
