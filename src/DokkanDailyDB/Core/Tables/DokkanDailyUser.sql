CREATE TABLE [Core].[DokkanDailyUser]
(
    [DokkanDailyUserId] INT NOT NULL PRIMARY KEY IDENTITY(1, 1),
    [DokkanNickname] VARCHAR(50) NULL INDEX DokkanDailyUser_IX01 WHERE [DokkanNickname] IS NOT NULL,
    [DiscordUsername] VARCHAR(50) NULL INDEX DokkanDailyUser_IX02 WHERE [DiscordUsername] IS NOT NULL,
    [DiscordId] VARCHAR(50) NULL INDEX DokkanDailyUser_IX03 WHERE [DiscordId] IS NOT NULL,
    CONSTRAINT [DokkanDailyUser_UC01] UNIQUE ([DokkanNickname], [DiscordUsername]) 
)