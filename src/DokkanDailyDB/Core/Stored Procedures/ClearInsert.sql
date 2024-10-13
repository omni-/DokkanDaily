CREATE PROCEDURE [Core].[ClearInsert]
	@Clears Core.ClearType READONLY,
	@ClearDate DATETIME2(2)
AS
BEGIN

	-- todo: merge ('foo', null) and (null, 'bar') when receiving ('foo', 'bar')

	MERGE INTO Core.DokkanDailyUser AS TARGET
	USING @Clears AS SOURCE
	ON (SOURCE.DokkanNickname = TARGET.DokkanNickname OR SOURCE.DiscordUsername = TARGET.DiscordUsername)
	WHEN MATCHED
	THEN UPDATE 
		SET TARGET.DokkanNickname = ISNULL(SOURCE.DokkanNickname, TARGET.DokkanNickname),
		TARGET.DiscordUsername = ISNULL(SOURCE.DiscordUsername, TARGET.DiscordUsername)
	WHEN NOT MATCHED BY TARGET THEN
		INSERT([DokkanNickname], [DiscordUsername])
		VALUES(SOURCE.[DokkanNickname], SOURCE.[DiscordUsername]);

	MERGE INTO Core.StageClear AS TARGET
	USING (
		SELECT 
			DDU.DokkanDailyUserId,
			C.ItemlessClear,
			C.ClearTime,
			@ClearDate AS ClearDate,
			C.IsDailyHighscore 
		FROM @Clears C 
		INNER JOIN Core.DokkanDailyUser DDU ON
		 C.DokkanNickname = DDU.DokkanNickname
		 OR C.DiscordUsername = DDU.DiscordUsername
	) AS SOURCE
	ON (SOURCE.DokkanDailyUserId = TARGET.DokkanDailyUserId AND SOURCE.ClearDate = TARGET.ClearDate)
	WHEN NOT MATCHED BY TARGET THEN
		INSERT(
			[DokkanDailyUserId],
			[ItemlessClear],
			[ClearTime],
			[ClearDate],
			[IsDailyHighscore])
		VALUES(
			SOURCE.DokkanDailyUserId,
			SOURCE.ItemlessClear,
			SOURCE.ClearTime,
			SOURCE.ClearDate,
			SOURCE.IsDailyHighscore);

RETURN 0
END