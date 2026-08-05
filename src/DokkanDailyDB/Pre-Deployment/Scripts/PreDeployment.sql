/* Remove historical duplicates before the one-clear-per-user-per-day constraint is applied. */
IF OBJECT_ID('Core.StageClear', 'U') IS NOT NULL
BEGIN
    ;WITH DuplicateClears AS (
        SELECT
            StageClearId,
            ROW_NUMBER() OVER (
                PARTITION BY DokkanDailyUserId, ClearDate
                ORDER BY IsDailyHighscore DESC, ItemlessClear DESC, StageClearId DESC) AS DuplicateRank
        FROM Core.StageClear WITH (TABLOCKX, HOLDLOCK)
    )
    DELETE FROM DuplicateClears WHERE DuplicateRank > 1;
END
GO
