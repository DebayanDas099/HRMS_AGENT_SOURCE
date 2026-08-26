IF OBJECT_ID(N'[dbo].[Get_Document_Mstr_By_Id]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[Get_Document_Mstr_By_Id];
GO

CREATE PROCEDURE [dbo].[Get_Document_Mstr_By_Id]
(
    @dm_id       BIGINT,
    @outputCode  INT           OUTPUT,
    @outputMsg   NVARCHAR(MAX) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT
            dm.dm_id AS [dm_id],
            dm.dm_category AS [dm_category],
            dm.dm_name AS [dm_name],
            dm.dm_path AS [dm_path],
            dm.dm_created_by AS [dm_created_by],
            dm.dm_created_date AS [dm_created_date],
            dm.dm_active AS [dm_active],
            dm.dm_ingestion_status AS [dm_ingestion_status],
            dm.dm_ingested_at AS [dm_ingested_at],
            dm.dm_ingestion_error AS [dm_ingestion_error],
            dm.dm_chunk_count AS [dm_chunk_count]
        FROM [dbo].[document_mstr] dm WITH (NOLOCK)
        WHERE dm.dm_id = @dm_id;

        SET @outputCode = 1;
        SET @outputMsg = N'Success';
    END TRY
    BEGIN CATCH
        SET @outputCode = -9;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
