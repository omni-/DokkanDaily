CREATE TYPE [Core].[ClearType] AS TABLE
(
	[DokkanNickname] VARCHAR(50) NOT NULL,
	[DiscordUsername] VARCHAR(50) NULL,
	[ItemlessClear] BIT NOT NULL,
	[ClearTime] VARCHAR(25) NOT NULL,
	[IsDailyHighscore] BIT NOT NULL
)
