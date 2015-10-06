CREATE TABLE [dbo].[MeasurementAudit] (
    [MeasurementAudit_ID] INT           IDENTITY (1, 1) NOT NULL,
    [Changed_DT]          DATETIME      DEFAULT (getdate()) NOT NULL,
    [Reason_TXT]          VARCHAR (256) NOT NULL,
    [Description_TXT]     VARCHAR (256) NOT NULL,
    [ChangedBy_NM]        VARCHAR (256)  NOT NULL,
    [Measurement_ID]      INT           NULL,
    PRIMARY KEY CLUSTERED ([MeasurementAudit_ID] ASC),
    CONSTRAINT [FK__Measureme__Measu__664B26CC] FOREIGN KEY ([Measurement_ID]) REFERENCES [dbo].[Measurement] ([Measurement_ID])
);


GO
ALTER TABLE [dbo].[MeasurementAudit] NOCHECK CONSTRAINT [FK__Measureme__Measu__664B26CC];

