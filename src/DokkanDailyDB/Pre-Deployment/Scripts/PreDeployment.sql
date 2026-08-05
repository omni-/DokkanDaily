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
    -- IncludeTransactionalScripts keeps this lock until the DACPAC finishes applying the schema
    -- plan. That prevents the currently running application from inserting another duplicate
    -- between this cleanup and creation of DailyChallenge_UC01.
    DELETE DC
    FROM Core.DailyChallenge DC WITH (TABLOCKX, HOLDLOCK)
    WHERE EXISTS (
        SELECT 1
        FROM Core.DailyChallenge Newer
        WHERE Newer.[Date] = DC.[Date]
          AND Newer.DailyChallengeId > DC.DailyChallengeId
    );
END
GO
