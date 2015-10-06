CREATE TABLE [dbo].[Goal] (
    [Goal_Id]             INT             IDENTITY (1, 1) NOT NULL,
    [Applies_After_Tmstp] DATETIME        NOT NULL,
    [Meets_Val]           DECIMAL (15, 4) NULL,
    [Meets_Plus_Val]      DECIMAL (15, 4) NULL,
    [Exceeds_Val]         DECIMAL (15, 4) NULL,
    [Wgt]                 INT             NOT NULL,
    [Typ]                 CHAR (2)        NOT NULL,
    [Sbmt_By]             VARCHAR (256)    NOT NULL,
    [Measurement_Id]      INT             NOT NULL,
    [GoalCategory_ID]     INT             DEFAULT ((1)) NOT NULL,
    PRIMARY KEY CLUSTERED ([Goal_Id] ASC),
    CONSTRAINT [Measurement.Measurement_Id] FOREIGN KEY ([Measurement_Id]) REFERENCES [dbo].[Measurement] ([Measurement_ID]),
    CONSTRAINT [GoalCategory.Id] FOREIGN KEY ([GoalCategory_ID]) REFERENCES [dbo].[GoalCategory] ([GoalCategory_ID])
);


GO
ALTER TABLE [dbo].[Goal] NOCHECK CONSTRAINT [Measurement.Measurement_Id];


GO
ALTER TABLE [dbo].[Goal] NOCHECK CONSTRAINT [GoalCategory.Id];


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Primary key for goal', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Goal', @level2type = N'COLUMN', @level2name = N'Goal_Id';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Timestamp this goal becomes active', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Goal', @level2type = N'COLUMN', @level2name = N'Applies_After_Tmstp';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Value a measurement has to be to be "meets"', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Goal', @level2type = N'COLUMN', @level2name = N'Meets_Val';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Value a measurement has to be to be "meets+"', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Goal', @level2type = N'COLUMN', @level2name = N'Meets_Plus_Val';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Value a measurement has to be to be "exceeds"', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Goal', @level2type = N'COLUMN', @level2name = N'Exceeds_Val';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Importance weight this goal has', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Goal', @level2type = N'COLUMN', @level2name = N'Wgt';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Type of goal (either "<=" or ">=")', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Goal', @level2type = N'COLUMN', @level2name = N'Typ';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'SSO of who submitted this goal', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Goal', @level2type = N'COLUMN', @level2name = N'Sbmt_By';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Foreign Key for Measurement ID', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Goal', @level2type = N'COLUMN', @level2name = N'Measurement_Id';

