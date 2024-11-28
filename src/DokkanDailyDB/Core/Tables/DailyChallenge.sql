CREATE TABLE [Core].[DailyChallenge]
(
    [DailyChallengeId] INT NOT NULL IDENTITY(1, 1),
    [DailyTypeId] INT NOT NULL,
    [Event] VARCHAR(50) NOT NULL,
    [Stage] INT NOT NULL,
    [Date] DATETIME2(2) NOT NULL,
    [LeaderFullName] VARCHAR(50) NULL,
    [Category] VARCHAR(25) NULL,
    [LinkSkill] VARCHAR(25) NULL,

    CONSTRAINT [DailyChallengePK] PRIMARY KEY CLUSTERED ([DailyChallengeId] ASC),
    CONSTRAINT [DailyChallenge_FK01] FOREIGN KEY([DailyTypeId]) REFERENCES [Core].[Daily]([DailyId]),
    CONSTRAINT [DailyChallenge_CHK01] CHECK (COALESCE([LeaderFullName], [Category], [LinkSkill]) IS NOT NULL)
)
