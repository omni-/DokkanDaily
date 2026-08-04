CREATE PROCEDURE [Core].[DailyInsert] 
  @Event VARCHAR(150),
  @Stage INT,
  @Date DATETIME2(2),
  @DailyTypeName VARCHAR(25),
  @LeaderFullName VARCHAR(200) = NULL,
  @Category VARCHAR(50) = NULL,
  @LinkSkill VARCHAR(50) = NULL
AS
BEGIN

    SET NOCOUNT ON;

    DECLARE @DailyTypeId INT;

    SELECT
        @DailyTypeId = DailyId
    FROM Core.Daily
    WHERE
        DailyTypeName = @DailyTypeName

    IF @DailyTypeId IS NULL
    BEGIN
        RAISERROR('Unknown daily type ''%s''. Core.Daily has no matching DailyTypeName.', 16, 1, @DailyTypeName);
        RETURN 1;
    END

    -- A restart or an admin rerun can invoke the reset twice in one day. Inserting a second row
    -- for the same date would skew the recency windows the challenge generator reads back, so
    -- treat a same-day insert as a no-op instead.
    IF EXISTS (SELECT 1 FROM [Core].[DailyChallenge] WHERE [Date] = @Date)
    BEGIN
        RETURN 0;
    END

    INSERT INTO [Core].[DailyChallenge](
        [Event], 
        [Stage], 
        [Date],
        [DailyTypeId],
        [LeaderFullName],
        [Category], 
        [LinkSkill])
    VALUES(
        @Event,
        @Stage,
        @Date,
        @DailyTypeId,
        @LeaderFullName,
        @Category, 
        @LinkSkill)

    RETURN 0;

END