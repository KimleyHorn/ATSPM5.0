-- Run against the ATSPM5 CONFIGURATION database, after the ConfigApi migration has run.
-- Safe to rerun: does not replace settings saved through the UI.
IF OBJECT_ID(N'dbo.WatchdogSettings', N'U') IS NULL
    THROW 50001, 'WatchdogSettings is missing. Start ConfigApi to apply migrations first.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.WatchdogSettings WHERE Id = 1)
BEGIN
    INSERT INTO dbo.WatchdogSettings (Id, SettingsJson, UpdatedAt)
    VALUES (1,
      N'{"ConsecutiveCount":3,"MinPhaseTerminations":50,"PercentThreshold":0.9,"AmStartHour":1,"AmEndHour":5,"PmPeakStartHour":16,"PmPeakEndHour":19,"MinimumRecords":500,"WeekdayOnly":false,"LowHitThreshold":50,"MaximumPedestrianEvents":25,"EmailAllErrors":true,"DefaultEmailAddress":"SPMWatchdog@friscotexas.gov"}',
      SYSDATETIMEOFFSET());
END;

SELECT Id, SettingsJson, UpdatedAt FROM dbo.WatchdogSettings WHERE Id = 1;
