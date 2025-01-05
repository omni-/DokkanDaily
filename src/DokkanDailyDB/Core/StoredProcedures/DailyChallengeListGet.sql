CREATE PROCEDURE [Core].[DailyChallengeListGet]
    @CutoffDateUTC DATETIME2
AS
BEGIN
    
    SELECT
        DC.[Event],
        DC.[Stage],
        D.[DailyTypeName],
        DC.[LeaderFullName],
        DC.[Category],
        DC.[LinkSkill]
    FROM Core.DailyChallenge DC
    INNER JOIN Core.Daily D ON
     DC.DailyTypeId = D.DailyId
    WHERE DC.[Date] > @CutoffDateUTC

    RETURN 0;
END