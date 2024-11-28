CREATE PROCEDURE [RefData].[LoadDaily]
AS
BEGIN

    INSERT INTO [Core].[Daily]([DailyTypeName])
    VALUES(
      'Character'), (
      'Category'), (
      'LinkSkill')

    RETURN 0;
END