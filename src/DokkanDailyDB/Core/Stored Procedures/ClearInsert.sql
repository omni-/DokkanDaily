CREATE PROCEDURE [Core].[ClearInsert]
	@Clears Core.ClearType READONLY,
	@ClearDate DATETIME2(2)
AS
BEGIN

	MERGE INTO Core.DokkanDailyUser AS TARGET
	USING @Clears AS SOURCE
	ON (SOURCE.DokkanNickname = TARGET.DokkanNickname)
	WHEN MATCHED 
	THEN UPDATE 
		SET TARGET.DokkanNickname = SOURCE.DokkanNickname,
		TARGET.DiscordUsername = SOURCE.DiscordUsername
	WHEN NOT MATCHED BY TARGET THEN
		INSERT([DokkanNickname], [DiscordUsername])
		VALUES(SOURCE.[DokkanNickname], SOURCE.[DiscordUsername]);

	INSERT INTO Core.StageClear (
		[DokkanDailyUserId],
		[ItemlessClear],
		[ClearTime],
		[ClearDate],
		[IsDailyHighscore])
	SELECT
		DDU.DokkanDailyUserId,
		C.ItemlessClear,
		C.ClearTime,
		@ClearDate,
		C.IsDailyHighscore
	FROM @Clears C 
	INNER JOIN Core.DokkanDailyUser DDU ON
	 C.DokkanNickname = DDU.DokkanNickname

RETURN 0
END