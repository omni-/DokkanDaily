/*
Post-Deployment Script Template                            
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be appended to the build script.        
 Use SQLCMD syntax to include a file in the post-deployment script.            
 Example:      :r .\myfile.sql                                
 Use SQLCMD syntax to reference a variable in the post-deployment script.        
 Example:      :setvar TableName MyTable                            
               SELECT * FROM [$(TableName)]                    
--------------------------------------------------------------------------------------
*/
EXEC RefData.LoadAllData
GO
-- The schema's permanent unique constraint now owns date uniqueness.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Core.DailyChallenge')
           AND name = 'DailyChallenge_MigrationDateGuard')
    DROP INDEX DailyChallenge_MigrationDateGuard ON Core.DailyChallenge;
GO
