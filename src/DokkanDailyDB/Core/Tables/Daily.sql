CREATE TABLE [Core].[Daily]
(
    [DailyId] INT NOT NULL,
    [DailyTypeName] VARCHAR(20) NOT NULL,

    CONSTRAINT [DailyPK] PRIMARY KEY CLUSTERED([DailyId] ASC)
)
