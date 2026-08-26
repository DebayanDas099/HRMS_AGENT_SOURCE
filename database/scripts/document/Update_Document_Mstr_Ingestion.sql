IF OBJECT_ID(N'[dbo].[Update_Document_Mstr_Ingestion]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[Update_Document_Mstr_Ingestion];
GO

CREATE PROCEDURE [dbo].[Update_Document_Mstr_Ingestion]
(
    @dm_id               BIGINT,
    @dm_ingestion_status VARCHAR(20),
    @dm_ingested_at      DATETIME      = NULL,
    @dm_ingestion_error  VARCHAR(MAX)  = NULL,
    @dm_chunk_count      INT           = NULL,
    @outputCode          INT           OUTPUT,
    @outputMsg           NVARCHAR(MAX) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM [dbo].[document_mstr] WITH (NOLOCK) WHERE dm_id = @dm_id)
        BEGIN
            SET @outputCode = -1;
            SET @outputMsg = N'Document not found.';
            RETURN;
        END

        UPDATE [dbo].[document_mstr]
        SET
            dm_ingestion_status = @dm_ingestion_status,
            dm_ingested_at = @dm_ingested_at,
            dm_ingestion_error = @dm_ingestion_error,
            dm_chunk_count = @dm_chunk_count
        WHERE dm_id = @dm_id;

        SET @outputCode = 1;
        SET @outputMsg = N'Success';
    END TRY
    BEGIN CATCH
        SET @outputCode = -9;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
