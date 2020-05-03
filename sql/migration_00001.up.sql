alter table `Update`
	add ChallengeId int default 0 not null;
alter table `Update` alter column ChallengeId drop default;

alter table Athlete modify Username text null;
