/*
    Returns document master list with optional category and search filters.
    Table: [dbo].[document_mstr]
*/

IF OBJECT_ID(N'[dbo].[Get_Document_Mstr_List]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[Get_Document_Mstr_List];
GO

CREATE PROCEDURE [dbo].[Get_Document_Mstr_List]
(
    @category     VARCHAR(100) = NULL,
    @search_text  VARCHAR(500) = NULL,
    @page_number  INT          = 1,
    @page_size    INT          = 10,
    @outputCode   INT          OUTPUT,
    @outputMsg    NVARCHAR(MAX) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SET @category = NULLIF(LTRIM(RTRIM(ISNULL(@category, ''))), '');
        SET @search_text = NULLIF(LTRIM(RTRIM(ISNULL(@search_text, ''))), '');
        SET @page_number = CASE WHEN ISNULL(@page_number, 0) < 1 THEN 1 ELSE @page_number END;
        SET @page_size = CASE
            WHEN ISNULL(@page_size, 0) < 1 THEN 10
            WHEN @page_size > 100 THEN 100
            ELSE @page_size
        END;

        DECLARE @offset INT = (@page_number - 1) * @page_size;

        SELECT COUNT(1) AS [total_count]
        FROM [dbo].[document_mstr] dm WITH (NOLOCK)
        WHERE (@category IS NULL OR UPPER(dm.dm_category) = UPPER(@category))
          AND (
                @search_text IS NULL
                OR dm.dm_name LIKE '%' + @search_text + '%'
                OR dm.dm_path LIKE '%' + @search_text + '%'
              );

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
        WHERE (@category IS NULL OR UPPER(dm.dm_category) = UPPER(@category))
          AND (
                @search_text IS NULL
                OR dm.dm_name LIKE '%' + @search_text + '%'
                OR dm.dm_path LIKE '%' + @search_text + '%'
              )
        ORDER BY dm.dm_created_date DESC, dm.dm_id DESC
        OFFSET @offset ROWS FETCH NEXT @page_size ROWS ONLY;

        SET @outputCode = 1;
        SET @outputMsg = N'Success';
    END TRY
    BEGIN CATCH
        SET @outputCode = -9;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
