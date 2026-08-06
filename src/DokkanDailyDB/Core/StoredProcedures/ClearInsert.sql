CREATE PROCEDURE [Core].[ClearInsert]
    @Clears Core.ClearType READONLY,
    @ClearDate DATETIME2(2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Reset is normally the only writer, so serializing this short operation is a cheap way
        -- to make admin reruns and overlapping workers safe across application instances.
        DECLARE @LockResult INT;
        EXEC @LockResult = sys.sp_getapplock
            @Resource = 'Core.ClearInsert',
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = 15000;

        IF @LockResult < 0
            THROW 51000, 'Could not acquire the clear-insert lock.', 1;

        DECLARE @Resolved TABLE (
            DokkanNickname VARCHAR(50) NULL,
            DiscordUsername VARCHAR(50) NULL,
            DiscordId VARCHAR(50) NULL,
            ItemlessClear BIT NOT NULL,
            ClearTime VARCHAR(25) NOT NULL,
            IsDailyHighscore BIT NOT NULL,
            DokkanDailyUserId INT NULL
        );

        INSERT INTO @Resolved (DokkanNickname, DiscordUsername, DiscordId, ItemlessClear, ClearTime, IsDailyHighscore, DokkanDailyUserId)
        SELECT
            C.DokkanNickname,
            C.DiscordUsername,
            C.DiscordId,
            C.ItemlessClear,
            C.ClearTime,
            C.IsDailyHighscore,
            M.DokkanDailyUserId
        FROM @Clears C
        OUTER APPLY (
            SELECT TOP 1 DDU.DokkanDailyUserId
            FROM Core.DokkanDailyUser DDU
            WHERE (C.DiscordId IS NOT NULL AND DDU.DiscordId = C.DiscordId)
               OR (C.DiscordId IS NOT NULL
                   AND C.DiscordUsername IS NOT NULL
                   AND DDU.DiscordId IS NULL
                   AND DDU.DiscordUsername = C.DiscordUsername)
               OR (C.DiscordId IS NULL
                   AND C.DiscordUsername IS NOT NULL
                   AND DDU.DiscordUsername = C.DiscordUsername)
               OR (C.DiscordId IS NULL
                   AND C.DokkanNickname IS NOT NULL
                   AND DDU.DokkanNickname = C.DokkanNickname)
            ORDER BY
                CASE
                    WHEN C.DiscordId IS NOT NULL AND DDU.DiscordId = C.DiscordId THEN 1
                    WHEN C.DiscordId IS NOT NULL AND DDU.DiscordId IS NULL THEN 2
                    WHEN C.DiscordUsername IS NOT NULL AND DDU.DiscordUsername = C.DiscordUsername THEN 3
                    WHEN C.DokkanNickname IS NOT NULL AND DDU.DokkanNickname = C.DokkanNickname THEN 4
                    ELSE 5
                END,
                DDU.DokkanDailyUserId
        ) M;

        UPDATE DDU
        SET DDU.DokkanNickname = ISNULL(R.DokkanNickname, DDU.DokkanNickname),
            DDU.DiscordUsername = ISNULL(R.DiscordUsername, DDU.DiscordUsername),
            DDU.DiscordId = ISNULL(R.DiscordId, DDU.DiscordId)
        FROM Core.DokkanDailyUser DDU
        INNER JOIN @Resolved R ON R.DokkanDailyUserId = DDU.DokkanDailyUserId;

        INSERT INTO Core.DokkanDailyUser ([DokkanNickname], [DiscordUsername], [DiscordId])
        SELECT DISTINCT R.DokkanNickname, R.DiscordUsername, R.DiscordId
        FROM @Resolved R
        WHERE R.DokkanDailyUserId IS NULL;

        UPDATE R
        SET R.DokkanDailyUserId = M.DokkanDailyUserId
        FROM @Resolved R
        OUTER APPLY (
            SELECT TOP 1 DDU.DokkanDailyUserId
            FROM Core.DokkanDailyUser DDU
            WHERE (R.DiscordId IS NOT NULL AND DDU.DiscordId = R.DiscordId)
               OR (R.DiscordId IS NULL AND R.DiscordUsername IS NOT NULL AND DDU.DiscordUsername = R.DiscordUsername)
               OR (R.DiscordId IS NULL AND R.DiscordUsername IS NULL AND DDU.DokkanNickname = R.DokkanNickname)
            ORDER BY DDU.DokkanDailyUserId
        ) M
        WHERE R.DokkanDailyUserId IS NULL;

        ;WITH Ranked AS (
            SELECT
                R.DokkanDailyUserId,
                R.ClearTime,
                R.IsDailyHighscore,
                MAX(CAST(R.ItemlessClear AS INT)) OVER (PARTITION BY R.DokkanDailyUserId) AS AnyItemless,
                ROW_NUMBER() OVER (
                    PARTITION BY R.DokkanDailyUserId
                    ORDER BY R.IsDailyHighscore DESC, R.ClearTime ASC) AS RowRank
            FROM @Resolved R
            WHERE R.DokkanDailyUserId IS NOT NULL
        )
        MERGE INTO Core.StageClear WITH (HOLDLOCK) AS TARGET
        USING (
            SELECT
                DokkanDailyUserId,
                CAST(AnyItemless AS BIT) AS ItemlessClear,
                ClearTime,
                @ClearDate AS ClearDate,
                IsDailyHighscore
            FROM Ranked
            WHERE RowRank = 1
        ) AS SOURCE
        ON SOURCE.DokkanDailyUserId = TARGET.DokkanDailyUserId
           AND SOURCE.ClearDate = TARGET.ClearDate
        WHEN NOT MATCHED BY TARGET THEN
            INSERT ([DokkanDailyUserId], [ItemlessClear], [ClearTime], [ClearDate], [IsDailyHighscore])
            VALUES (SOURCE.DokkanDailyUserId, SOURCE.ItemlessClear, SOURCE.ClearTime, SOURCE.ClearDate, SOURCE.IsDailyHighscore)
        WHEN MATCHED THEN
            UPDATE SET
                TARGET.ItemlessClear = SOURCE.ItemlessClear,
                TARGET.ClearTime = SOURCE.ClearTime,
                TARGET.IsDailyHighscore = SOURCE.IsDailyHighscore;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    RETURN 0;
END
