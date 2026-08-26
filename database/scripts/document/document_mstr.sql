/*
    Document master table for HRMS Chatbot document repository.
    Run against APPDB (or target HRMS SQL database).
*/

IF OBJECT_ID(N'[dbo].[document_mstr]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[document_mstr](
        [dm_id] [bigint] IDENTITY(1,1) NOT NULL,
        [dm_category] [varchar](100) NOT NULL,
        [dm_name] [varchar](500) NOT NULL,
        [dm_path] [varchar](max) NOT NULL,
        [dm_created_by] [varchar](20) NOT NULL,
        [dm_created_date] [datetime] NOT NULL,
        [dm_active] [char](1) NOT NULL,
        CONSTRAINT [PK_document_mstr] PRIMARY KEY CLUSTERED ([dm_id] ASC)
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO
