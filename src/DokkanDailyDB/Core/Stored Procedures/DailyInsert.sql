CREATE PROCEDURE [Core.[DailyInsert] 
  @Event VARCHAR(50),
  @Stage INT,
  @Date DATETIME2(2),
  @DailyTypeName VARCHAR(25),
  @LeaderFullName VARCHAR(50) = NULL,
  @Category VARCHAR(25) = NULL, 
  @LinkSkill VARCHAR(25) = NULL
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