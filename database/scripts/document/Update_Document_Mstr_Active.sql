/*
    Updates active flag for a document master record.
    Table: [dbo].[document_mstr]
*/

IF OBJECT_ID(N'[dbo].[Update_Document_Mstr_Active]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[Update_Document_Mstr_Active];
GO

CREATE PROCEDURE [dbo].[Update_Document_Mstr_Active]
(
    @dm_id       BIGINT,
    @dm_active   CHAR(1),
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

        SET @dm_active = UPPER(LTRIM(RTRIM(ISNULL(@dm_active, ''))));

        IF @dm_active NOT IN ('Y', 'N')
        BEGIN
            SET @outputCode = -2;
            SET @outputMsg = N'Active flag must be Y or N.';
            RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM [dbo].[document_mstr] WITH (NOLOCK) WHERE dm_id = @dm_id)
        BEGIN
            SET @outputCode = -3;
            SET @outputMsg = N'Document not found.';
            RETURN;
        END

        UPDATE [dbo].[document_mstr]
        SET [dm_active] = @dm_active
        WHERE [dm_id] = @dm_id;

        SET @outputCode = 1;
        SET @outputMsg = N'Document status updated successfully.';
    END TRY
    BEGIN CATCH
        SET @outputCode = -9;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
