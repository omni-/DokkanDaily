CREATE PROCEDURE [RefData].[LoadDaily]
AS
BEGIN

    DELETE FROM [Core].[Daily];

    INSERT INTO [Core].[Daily]([DailyTypeName])
    VALUES(
      'Character'), (
      'Category'), (
      'LinkSkill')

    RETURN 0;
END