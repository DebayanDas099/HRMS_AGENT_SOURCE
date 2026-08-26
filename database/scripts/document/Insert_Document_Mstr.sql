/*
    Inserts a document master record after blob upload.
    Table: [dbo].[document_mstr]
*/

IF OBJECT_ID(N'[dbo].[Insert_Document_Mstr]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[Insert_Document_Mstr];
GO

CREATE PROCEDURE [dbo].[Insert_Document_Mstr]
(
    @dm_category    VARCHAR(100),
    @dm_name        VARCHAR(500),
    @dm_path        VARCHAR(MAX),
    @dm_created_by  VARCHAR(20),
    @dm_id          BIGINT         OUTPUT,
    @outputCode     INT            OUTPUT,
    @outputMsg      NVARCHAR(MAX)  OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SET @dm_category = NULLIF(LTRIM(RTRIM(ISNULL(@dm_category, ''))), '');
        SET @dm_name = NULLIF(LTRIM(RTRIM(ISNULL(@dm_name, ''))), '');
        SET @dm_path = NULLIF(LTRIM(RTRIM(ISNULL(@dm_path, ''))), '');
        SET @dm_created_by = NULLIF(LTRIM(RTRIM(ISNULL(@dm_created_by, ''))), '');

        IF @dm_category IS NULL
        BEGIN
            SET @outputCode = -1;
            SET @outputMsg = N'Document category is required.';
            RETURN;
        END

        IF @dm_name IS NULL
        BEGIN
            SET @outputCode = -2;
            SET @outputMsg = N'Document name is required.';
            RETURN;
        END

        IF @dm_path IS NULL
        BEGIN
            SET @outputCode = -3;
            SET @outputMsg = N'Document path is required.';
            RETURN;
        END

        IF @dm_created_by IS NULL
        BEGIN
            SET @outputCode = -4;
            SET @outputMsg = N'Created by is required.';
            RETURN;
        END

        IF UPPER(@dm_category) NOT IN ('POLICY', 'TRAINING')
        BEGIN
            SET @outputCode = -5;
            SET @outputMsg = N'Invalid document category. Allowed values: Policy, Training.';
            RETURN;
        END

        INSERT INTO [dbo].[document_mstr]
        (
            [dm_category],
            [dm_name],
            [dm_path],
            [dm_created_by],
            [dm_created_date],
            [dm_active]
        )
        VALUES
        (
            @dm_category,
            @dm_name,
            @dm_path,
            @dm_created_by,
            GETDATE(),
            'Y'
        );

        SET @dm_id = CAST(SCOPE_IDENTITY() AS BIGINT);
        SET @outputCode = 1;
        SET @outputMsg = N'Document saved successfully.';
    END TRY
    BEGIN CATCH
        SET @outputCode = -9;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
