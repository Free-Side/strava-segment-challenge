
alter table Challenge add UseMovingTime bit default b'0' not null;

alter table Challenge add GpxData mediumtext null;
