CREATE PROCEDURE [Core].[DailyInsert] 
  @Event VARCHAR(150),
  @Stage INT,
  @Date DATETIME2(2),
  @DailyTypeName VARCHAR(25),
  @LeaderFullName VARCHAR(100) = NULL,
  @Category VARCHAR(50) = NULL, 
  @LinkSkill VARCHAR(50) = NULL
AS
BEGIN

    DECLARE @DailyTypeId INT;

    SELECT
        @DailyTypeId = DailyId
    FROM Core.Daily
    WHERE
        DailyTypeName = @DailyTypeName

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