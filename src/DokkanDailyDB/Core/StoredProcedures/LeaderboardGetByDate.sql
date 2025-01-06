CREATE PROCEDURE [Core].[LeaderboardGetByDate]
    @MonthAndYear DATETIME2(2)
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
        YEAR(C.ClearDate) = YEAR(@MonthAndYear)
        AND MONTH(C.ClearDate) = MONTH(@MonthAndYear)
    GROUP BY
        DDU.DokkanNickname,
        DDU.DiscordUsername

RETURN 0
END