CREATE PROCEDURE [RefData].[LoadDaily]
AS
BEGIN

    DECLARE @Source TABLE (
        DailyId INT NOT NULL, 
        DailyTypeName VARCHAR(20) NOT NULL
    )

    INSERT INTO @Source
    VALUES(
      1, 'Character'), (
      2, 'Category'), (
      3, 'LinkSkill')

    MERGE INTO [Core].[Daily] AS TARGET
    USING @Source AS SOURCE
    ON TARGET.DailyId = SOURCE.DailyId
    WHEN MATCHED AND (
        TARGET.DailyTypeName COLLATE SQL_Latin1_General_CP1_CS_AS <> SOURCE.DailyTypeName COLLATE SQL_Latin1_General_CP1_CS_AS) THEN
      UPDATE SET
        DailyTypeName = TARGET.DailyTypeName
    WHEN NOT MATCHED BY TARGET THEN
      INSERT (
        DailyId,
        DailyTypeName)
      VALUES (
        DailyId,
        DailyTypeName)
    WHEN NOT MATCHED BY SOURCE THEN 
      DELETE;

END