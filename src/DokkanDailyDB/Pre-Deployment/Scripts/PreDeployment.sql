/*
Pre-Deployment Script
--------------------------------------------------------------------------------------
 Runs before the schema changes in this package are applied.
--------------------------------------------------------------------------------------
*/

-- Core.DailyChallenge gains a UNIQUE constraint on [Date] in this release, which cannot be
-- created while duplicate dates exist. Duplicates were possible while DailyInsert was a plain
-- INSERT, so collapse any historical ones first, keeping the most recently inserted row for
-- each date. On a fresh database the table does not exist yet and this is skipped.
IF OBJECT_ID('Core.DailyChallenge', 'U') IS NOT NULL
BEGIN
    DELETE DC
    FROM Core.DailyChallenge DC
    WHERE EXISTS (
        SELECT 1
        FROM Core.DailyChallenge Newer
        WHERE Newer.[Date] = DC.[Date]
          AND Newer.DailyChallengeId > DC.DailyChallengeId
    );
END
GO
