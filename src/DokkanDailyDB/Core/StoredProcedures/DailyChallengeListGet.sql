CREATE PROCEDURE [Core].[DailyChallengeListGet]
    @CutoffDateUTC DATETIME2 = NULL
AS
BEGIN
    
    SELECT
        DC.[Event],
        DC.[Stage],
        D.[DailyTypeName],
        DC.[Date],
        DC.[LeaderFullName],
        DC.[Category],
        DC.[LinkSkill]
    FROM Core.DailyChallenge DC
    INNER JOIN Core.Daily D ON
     DC.DailyTypeId = D.DailyId
    WHERE @CutoffDateUTC IS NULL OR DC.[Date] > @CutoffDateUTC
    ORDER BY DC.[Date] DESC

    RETURN 0;
END