CREATE TABLE [Core].[DokkanDailyUser]
(
	[DokkanDailyUserId] INT NOT NULL PRIMARY KEY IDENTITY(1, 1),
	[DokkanNickname] VARCHAR(50) NOT NULL,
	[DiscordUsername] VARCHAR(50) NULL,
	CONSTRAINT [DokkanDailyUser_UC01] UNIQUE ([DokkanNickname]) 
)
