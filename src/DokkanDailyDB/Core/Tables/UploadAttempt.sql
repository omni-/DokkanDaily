CREATE TABLE [Core].[UploadAttempt]
(
    [UploaderKey] NVARCHAR(100) NOT NULL,
    [AttemptDate] DATE NOT NULL,
    [AttemptCount] TINYINT NOT NULL,
    CONSTRAINT [UploadAttempt_PK] PRIMARY KEY CLUSTERED ([UploaderKey], [AttemptDate]),
    CONSTRAINT [UploadAttempt_CK01] CHECK ([AttemptCount] BETWEEN 1 AND 5)
);
