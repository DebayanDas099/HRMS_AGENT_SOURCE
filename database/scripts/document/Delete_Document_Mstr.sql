/*
    Deletes a document master record permanently.
    Table: [dbo].[document_mstr]
*/

IF OBJECT_ID(N'[dbo].[Delete_Document_Mstr]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[Delete_Document_Mstr];
GO

CREATE PROCEDURE [dbo].[Delete_Document_Mstr]
(
    @dm_id       BIGINT,
    @outputCode  INT           OUTPUT,
    @outputMsg   NVARCHAR(MAX) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF @dm_id IS NULL OR @dm_id <= 0
        BEGIN
            SET @outputCode = -1;
            SET @outputMsg = N'Invalid document id.';
            RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM [dbo].[document_mstr] WITH (NOLOCK) WHERE dm_id = @dm_id)
        BEGIN
            SET @outputCode = -3;
            SET @outputMsg = N'Document not found.';
            RETURN;
        END

        DELETE FROM [dbo].[document_mstr]
        WHERE [dm_id] = @dm_id;

        SET @outputCode = 1;
        SET @outputMsg = N'Document deleted successfully.';
    END TRY
    BEGIN CATCH
        SET @outputCode = -9;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
