CREATE TABLE [dbo].[Datapoint] (
    [Datapoint_ID]    INT             IDENTITY (1, 1) NOT NULL,
    [HasSubmitted_IN] SMALLINT        DEFAULT ((0)) NOT NULL,
    [Applicable_DT]   DATE            NOT NULL,
    [Created_DT]      DATE            DEFAULT (CONVERT([date],getdate())) NOT NULL,
    [Created_TM]      TIME (7)        DEFAULT (CONVERT([time],getdate())) NOT NULL,
    [Value_AMT]       DECIMAL (15, 4) NOT NULL,
    [Measurement_ID]  INT             NOT NULL,
    [Sbmt_By]         VARCHAR (999)    DEFAULT ('CBSH\Temp') NOT NULL,
    PRIMARY KEY CLUSTERED ([Datapoint_ID] ASC),
    CONSTRAINT [FK__Datapoint__Measu__44952D46] FOREIGN KEY ([Measurement_ID]) REFERENCES [dbo].[Measurement] ([Measurement_ID])
);


GO
ALTER TABLE [dbo].[Datapoint] NOCHECK CONSTRAINT [FK__Datapoint__Measu__44952D46];

