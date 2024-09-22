CREATE TABLE [Core].[StageClear]
(
	[StageClearId] INT NOT NULL IDENTITY(1, 1),
	[DokkanDailyUserId] INT NOT NULL,
	[ItemlessClear] BIT NOT NULL,
	[IsDailyHighscore] BIT NOT NULL,
	[ClearTime] VARCHAR(25) NOT NULL,
	[ClearDate] DATETIME2(2) NOT NULL,
	CONSTRAINT [ClearPK] PRIMARY KEY CLUSTERED ([StageClearId] ASC),
	CONSTRAINT [Clear_FK01] FOREIGN KEY ([DokkanDailyUserId]) REFERENCES [Core].[DokkanDailyUser]([DokkanDailyUserId])
)
