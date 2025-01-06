CREATE PROCEDURE [Core].[HallOfFameLeaderboardGet]
AS
BEGIN

    SELECT
        DDU.DokkanNickname,
        DDU.DiscordUsername,
        COUNT(*) AS TotalClears,
        COUNT(CASE WHEN C.ItemlessClear = 1 THEN 1 ELSE NULL END) AS ItemlessClears,
        COUNT(CASE WHEN C.IsDailyHighscore = 1 THEN 1 ELSE NULL END) AS DailyHighscores 
    FROM [Core].[StageClear] C
    INNER JOIN [Core].[DokkanDailyUser] DDU ON
     C.DokkanDailyUserId = DDU.DokkanDailyUserId
    WHERE
        C.ClearDate < CAST('1/6/2025 00:00' AS DATETIME2) --date of implementation for improved OCR
    GROUP BY
        DDU.DokkanNickname,
        DDU.DiscordUsername

RETURN 0
END