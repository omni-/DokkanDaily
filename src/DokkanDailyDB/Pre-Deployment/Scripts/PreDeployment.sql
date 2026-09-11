/* Remove historical duplicates before the one-clear-per-user-per-day constraint is applied. */
IF OBJECT_ID('Core.StageClear', 'U') IS NOT NULL
BEGIN
    DECLARE @MergedClears TABLE
    (
        KeeperStageClearId INT NOT NULL,
        DokkanDailyUserId INT NOT NULL,
        ClearDate DATETIME2(2) NOT NULL,
        ItemlessClear BIT NOT NULL,
        IsDailyHighscore BIT NOT NULL,
        ClearTime VARCHAR(25) NOT NULL
    );

    INSERT INTO @MergedClears
        (KeeperStageClearId, DokkanDailyUserId, ClearDate, ItemlessClear, IsDailyHighscore, ClearTime)
    SELECT
        MAX(StageClearId),
        DokkanDailyUserId,
        ClearDate,
        CAST(MAX(CAST(ItemlessClear AS TINYINT)) AS BIT),
        CAST(MAX(CAST(IsDailyHighscore AS TINYINT)) AS BIT),
        MIN(ClearTime)
    FROM Core.StageClear WITH (TABLOCKX, HOLDLOCK)
    GROUP BY DokkanDailyUserId, ClearDate;

    UPDATE C
    SET C.ItemlessClear = M.ItemlessClear,
        C.IsDailyHighscore = M.IsDailyHighscore,
        C.ClearTime = M.ClearTime
    FROM Core.StageClear C
    INNER JOIN @MergedClears M ON M.KeeperStageClearId = C.StageClearId;

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

-- Install the unique date index in the cleanup transaction itself, closing the gap before
-- SqlPackage's schema transaction. Keep the newest record for each calendar date.
IF OBJECT_ID('Core.DailyChallenge', 'U') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        ;WITH Ranked AS (
            SELECT *, ROW_NUMBER() OVER (
                PARTITION BY CONVERT(DATE, [Date]) ORDER BY DailyChallengeId DESC) AS DuplicateRank
            FROM Core.DailyChallenge WITH (TABLOCKX, HOLDLOCK)
        )
        DELETE FROM Ranked WHERE DuplicateRank > 1;
        UPDATE Core.DailyChallenge SET [Date] = CONVERT(DATE, [Date]);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID('Core.DailyChallenge') AND name = 'DailyChallenge_MigrationDateGuard')
            CREATE UNIQUE INDEX DailyChallenge_MigrationDateGuard ON Core.DailyChallenge ([Date]);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
