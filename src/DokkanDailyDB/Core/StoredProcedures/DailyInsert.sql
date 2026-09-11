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
    SET XACT_ABORT ON;
    SET @Date = CONVERT(DATE, @Date);

    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @DailyTypeId INT;
        SELECT @DailyTypeId = DailyId FROM Core.Daily WHERE DailyTypeName = @DailyTypeName;
        IF @DailyTypeId IS NULL
            THROW 51000, 'Unknown daily type.', 1;

        -- Range lock plus the unique date key serializes concurrent retries, including absent dates.
        UPDATE Core.DailyChallenge WITH (UPDLOCK, HOLDLOCK)
        SET [Event] = @Event, Stage = @Stage, DailyTypeId = @DailyTypeId,
            LeaderFullName = @LeaderFullName, Category = @Category, LinkSkill = @LinkSkill
        WHERE [Date] = @Date;

        IF @@ROWCOUNT = 0
            INSERT Core.DailyChallenge ([Event], Stage, [Date], DailyTypeId, LeaderFullName, Category, LinkSkill)
            VALUES (@Event, @Stage, @Date, @DailyTypeId, @LeaderFullName, @Category, @LinkSkill);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
    RETURN 0;
END
