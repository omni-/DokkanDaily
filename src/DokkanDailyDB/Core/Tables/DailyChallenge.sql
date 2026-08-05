CREATE TABLE [Core].[DailyChallenge]
(
    [DailyChallengeId] INT NOT NULL IDENTITY(1, 1),
    [DailyTypeId] INT NOT NULL,
    -- widened to match the stored procedure parameters; long event and leader names would
    -- otherwise fail the insert outright. Existing rows stay truncated, which is why the
    -- challenge generator still matches these columns with StartsWith rather than equality.
    [Event] VARCHAR(150) NOT NULL,
    [Stage] INT NOT NULL,
    [Date] DATETIME2(2) NOT NULL,
    [LeaderFullName] VARCHAR(200) NULL,
    [Category] VARCHAR(50) NULL,
    [LinkSkill] VARCHAR(50) NULL,

    CONSTRAINT [DailyChallengePK] PRIMARY KEY CLUSTERED ([DailyChallengeId] ASC),
    -- one challenge per day, enforced by the engine rather than by a check-then-insert that two
    -- scaled-out Worker instances could both pass. Also serves DailyChallengeListGet's date sort.
    CONSTRAINT [DailyChallenge_UC01] UNIQUE ([Date]),
    CONSTRAINT [DailyChallenge_FK01] FOREIGN KEY([DailyTypeId]) REFERENCES [Core].[Daily]([DailyId]),
    CONSTRAINT [DailyChallenge_CHK01] CHECK (COALESCE([LeaderFullName], [Category], [LinkSkill]) IS NOT NULL)
)
