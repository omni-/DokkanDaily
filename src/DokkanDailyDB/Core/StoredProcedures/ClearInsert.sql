CREATE PROCEDURE [Core].[ClearInsert]
    @Clears Core.ClearType READONLY,
    @ClearDate DATETIME2(2)
AS
BEGIN

    SET NOCOUNT ON;

    -- Resolve every incoming clear to at most one existing user before touching anything.
    -- Identity is matched in priority order (DiscordId, then DiscordUsername, then DokkanNickname)
    -- and TOP 1 guarantees a single target row per clear. Joining a MERGE on an OR - as this
    -- procedure used to - can match several target rows for one source row and fails the whole
    -- batch with error 8672, "attempted to UPDATE or DELETE the same row more than once".
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
        SELECT TOP 1
            DDU.DokkanDailyUserId
        FROM Core.DokkanDailyUser DDU
        WHERE (C.DiscordId IS NOT NULL AND DDU.DiscordId = C.DiscordId)
           OR (C.DiscordUsername IS NOT NULL AND DDU.DiscordUsername = C.DiscordUsername)
           OR (C.DokkanNickname IS NOT NULL AND DDU.DokkanNickname = C.DokkanNickname)
        ORDER BY
            CASE
                WHEN C.DiscordId IS NOT NULL AND DDU.DiscordId = C.DiscordId THEN 1
                WHEN C.DiscordUsername IS NOT NULL AND DDU.DiscordUsername = C.DiscordUsername THEN 2
                ELSE 3
            END,
            DDU.DokkanDailyUserId
    ) M;

    -- backfill any identity fields we have newly learned about an already known user
    UPDATE DDU
    SET DDU.DokkanNickname = ISNULL(R.DokkanNickname, DDU.DokkanNickname),
        DDU.DiscordUsername = ISNULL(R.DiscordUsername, DDU.DiscordUsername),
        DDU.DiscordId = ISNULL(R.DiscordId, DDU.DiscordId)
    FROM Core.DokkanDailyUser DDU
    INNER JOIN @Resolved R ON
     R.DokkanDailyUserId = DDU.DokkanDailyUserId;

    INSERT INTO Core.DokkanDailyUser ([DokkanNickname], [DiscordUsername], [DiscordId])
    SELECT DISTINCT
        R.DokkanNickname,
        R.DiscordUsername,
        R.DiscordId
    FROM @Resolved R
    WHERE R.DokkanDailyUserId IS NULL;

    -- pick up the ids of the users we just created
    UPDATE R
    SET R.DokkanDailyUserId = DDU.DokkanDailyUserId
    FROM @Resolved R
    INNER JOIN Core.DokkanDailyUser DDU ON
     (R.DiscordId IS NOT NULL AND DDU.DiscordId = R.DiscordId)
     OR (R.DiscordId IS NULL AND R.DiscordUsername IS NOT NULL AND DDU.DiscordUsername = R.DiscordUsername)
     OR (R.DiscordId IS NULL AND R.DiscordUsername IS NULL AND DDU.DokkanNickname = R.DokkanNickname)
    WHERE R.DokkanDailyUserId IS NULL;

    -- Only one clear per user per day is stored, so collapse to a single representative row here
    -- as well. The caller already does this, but a duplicate slipping through would otherwise
    -- match the same target row twice and fail the MERGE. The itemless flag is OR'd across the
    -- whole day so a user does not lose the itemless point to their own faster run.
    WITH Ranked AS (
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
    MERGE INTO Core.StageClear AS TARGET
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
    ON (SOURCE.DokkanDailyUserId = TARGET.DokkanDailyUserId AND SOURCE.ClearDate = TARGET.ClearDate)
    WHEN NOT MATCHED BY TARGET THEN
        INSERT(
            [DokkanDailyUserId],
            [ItemlessClear],
            [ClearTime],
            [ClearDate],
            [IsDailyHighscore])
        VALUES(
            SOURCE.DokkanDailyUserId,
            SOURCE.ItemlessClear,
            SOURCE.ClearTime,
            SOURCE.ClearDate,
            SOURCE.IsDailyHighscore)
     WHEN MATCHED THEN
     UPDATE SET
         TARGET.ItemlessClear = SOURCE.ItemlessClear
         , TARGET.ClearTime = SOURCE.ClearTime
         , Target.IsDailyHighscore = SOURCE.IsDailyHighscore;

RETURN 0
END
