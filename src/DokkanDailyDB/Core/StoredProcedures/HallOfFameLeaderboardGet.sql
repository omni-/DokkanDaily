CREATE PROCEDURE [Core].[HallOfFameLeaderboardGet]
AS
BEGIN

    SELECT
        DDU.DokkanNickname,
        DDU.DiscordUsername,
        DDU.DiscordId,
        COUNT(*) AS TotalClears,
        COUNT(CASE WHEN C.ItemlessClear = 1 THEN 1 ELSE NULL END) AS ItemlessClears,
        COUNT(CASE WHEN C.IsDailyHighscore = 1 THEN 1 ELSE NULL END) AS DailyHighscores
    FROM [Core].[StageClear] C
    INNER JOIN [Core].[DokkanDailyUser] DDU ON
     C.DokkanDailyUserId = DDU.DokkanDailyUserId
    WHERE
        -- ISO-8601, so the comparison cannot be reinterpreted under a non-MDY DATEFORMAT
        C.ClearDate < CAST('2025-01-06T00:00:00' AS DATETIME2) --date of implementation for improved OCR
    GROUP BY
        DDU.DokkanNickname,
        DDU.DiscordUsername,
        DDU.DiscordId

RETURN 0
END