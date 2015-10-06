CREATE TABLE [dbo].[MonthlyEntriesAudit] (
    [Audit_ID]     INT             IDENTITY (1, 1) NOT NULL,
    [ChangedBy_NM] VARCHAR (256)    NOT NULL,
    [OldValue_AMT] DECIMAL (15, 4) NOT NULL,
    [NewValue_AMT] DECIMAL (15, 4) NOT NULL,
    [Edited_DT]    DATETIME        DEFAULT (getdate()) NOT NULL,
    [Reason_TXT]   VARCHAR (256)   NOT NULL,
    [Datapoint_ID] INT             NOT NULL,
    PRIMARY KEY CLUSTERED ([Audit_ID] ASC),
    CONSTRAINT [FK__MonthlyEn__Datap__60C757A0] FOREIGN KEY ([Datapoint_ID]) REFERENCES [dbo].[Datapoint] ([Datapoint_ID])
);


GO
ALTER TABLE [dbo].[MonthlyEntriesAudit] NOCHECK CONSTRAINT [FK__MonthlyEn__Datap__60C757A0];

