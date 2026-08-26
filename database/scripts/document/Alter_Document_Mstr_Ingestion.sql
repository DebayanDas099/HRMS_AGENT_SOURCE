/*
    Adds ingestion tracking columns to document_mstr.
*/

IF COL_LENGTH('[dbo].[document_mstr]', 'dm_ingestion_status') IS NULL
BEGIN
    ALTER TABLE [dbo].[document_mstr]
    ADD [dm_ingestion_status] VARCHAR(20) NOT NULL
        CONSTRAINT [DF_document_mstr_ingestion_status] DEFAULT ('Pending');
END
GO

IF COL_LENGTH('[dbo].[document_mstr]', 'dm_ingested_at') IS NULL
BEGIN
    ALTER TABLE [dbo].[document_mstr]
    ADD [dm_ingested_at] DATETIME NULL;
END
GO

IF COL_LENGTH('[dbo].[document_mstr]', 'dm_ingestion_error') IS NULL
BEGIN
    ALTER TABLE [dbo].[document_mstr]
    ADD [dm_ingestion_error] VARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH('[dbo].[document_mstr]', 'dm_chunk_count') IS NULL
BEGIN
    ALTER TABLE [dbo].[document_mstr]
    ADD [dm_chunk_count] INT NULL;
END
GO
