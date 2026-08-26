/*
    Returns aggregate document statistics for dashboard and repository header.
    Table: [dbo].[document_mstr]
*/

IF OBJECT_ID(N'[dbo].[Get_Document_Mstr_Statistics]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[Get_Document_Mstr_Statistics];
GO

CREATE PROCEDURE [dbo].[Get_Document_Mstr_Statistics]
(
    @outputCode INT           OUTPUT,
    @outputMsg  NVARCHAR(MAX) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT
            COUNT(1) AS [total_documents],
            SUM(CASE WHEN UPPER(LTRIM(RTRIM(ISNULL(dm_category, '')))) = 'POLICY' THEN 1 ELSE 0 END) AS [policy_documents],
            SUM(CASE WHEN UPPER(LTRIM(RTRIM(ISNULL(dm_category, '')))) = 'TRAINING' THEN 1 ELSE 0 END) AS [training_documents],
            SUM(CASE WHEN ISNULL(dm_active, 'N') = 'Y' THEN 1 ELSE 0 END) AS [active_documents],
            SUM(CASE WHEN ISNULL(dm_active, 'N') <> 'Y' THEN 1 ELSE 0 END) AS [inactive_documents]
        FROM [dbo].[document_mstr] WITH (NOLOCK);

        SET @outputCode = 1;
        SET @outputMsg = N'Success';
    END TRY
    BEGIN CATCH
        SET @outputCode = -9;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
