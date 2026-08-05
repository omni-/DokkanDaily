CREATE PROCEDURE [Core].[UploadAttemptTryAccept]
    @UploaderKey NVARCHAR(100),
    @AttemptDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Accepted BIT = 0;
    DECLARE @LockResult INT;
    DECLARE @LockResource NVARCHAR(255) = CONCAT(N'DokkanDaily.UploadAttempt:', @AttemptDate, N':', @UploaderKey);

    BEGIN TRY
        BEGIN TRANSACTION;

    -- Serialize this uploader/day across every application instance. The conditional UPDATE is
    -- the admission decision; no application-side or SQL check-then-increment race is possible.
        EXEC @LockResult = sys.sp_getapplock
            @Resource = @LockResource,
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = 15000;

        IF @LockResult < 0
            THROW 50001, 'Could not acquire the upload-attempt admission lock.', 1;

        UPDATE [Core].[UploadAttempt]
        SET [AttemptCount] = [AttemptCount] + 1
        WHERE [UploaderKey] = @UploaderKey
          AND [AttemptDate] = @AttemptDate
          AND [AttemptCount] < 5;

        IF @@ROWCOUNT = 1
        BEGIN
            SET @Accepted = 1;
        END
        ELSE IF NOT EXISTS
        (
            SELECT 1
            FROM [Core].[UploadAttempt]
            WHERE [UploaderKey] = @UploaderKey
              AND [AttemptDate] = @AttemptDate
        )
        BEGIN
            INSERT [Core].[UploadAttempt] ([UploaderKey], [AttemptDate], [AttemptCount])
            VALUES (@UploaderKey, @AttemptDate, 1);

            SET @Accepted = 1;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;

    SELECT @Accepted AS [Accepted];
END;
