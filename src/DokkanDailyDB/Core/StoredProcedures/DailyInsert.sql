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

    -- Upsert on the date. A restart or a rerun can invoke the reset more than once for the same
    -- day; a second plain INSERT would leave duplicate rows and skew the recency windows the
    -- challenge generator reads back. Replacing keeps one row per day AND lets a rerun correct a
    -- challenge that was regenerated after the first insert.
    --
    -- HOLDLOCK is what makes this safe when more than one instance is running: every instance
    -- hosts its own Worker, so two can reach this at 23:59 together. Under the UNIQUE constraint
    -- on [Date] the hint takes a range lock on the key, serialising the match-then-write instead
    -- of letting both miss and both insert. An UPDATE followed by a conditional INSERT has that
    -- same race and is not sufficient.
    MERGE INTO [Core].[DailyChallenge] WITH (HOLDLOCK) AS TARGET
    USING (SELECT @Date AS [Date]) AS SOURCE
    ON (TARGET.[Date] = SOURCE.[Date])
    WHEN MATCHED THEN
        UPDATE SET
            TARGET.[Event] = @Event,
            TARGET.[Stage] = @Stage,
            TARGET.[DailyTypeId] = @DailyTypeId,
            TARGET.[LeaderFullName] = @LeaderFullName,
            TARGET.[Category] = @Category,
            TARGET.[LinkSkill] = @LinkSkill
    WHEN NOT MATCHED BY TARGET THEN
        INSERT(
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
            @LinkSkill);

    RETURN 0;

END