﻿CREATE TABLE [Core].[Daily]
(
    [DailyId] INT NOT NULL IDENTITY(1, 1),
    [DailyTypeName] VARCHAR(20) NOT NULL,

    CONSTRAINT [DailyPK] PRIMARY KEY CLUSTERED([DailyId] ASC)
)