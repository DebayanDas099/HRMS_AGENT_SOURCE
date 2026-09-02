/*
    Returns top document matches by similarity score for name-based lookup.
    Uses [dbo].[GetSimilarityScore] and avoids LIKE predicates.
*/

IF OBJECT_ID(N'[dbo].[Get_Document_Mstr_By_Similarity]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[Get_Document_Mstr_By_Similarity];
GO

CREATE PROCEDURE [dbo].[Get_Document_Mstr_By_Similarity]
(
    @search_text  VARCHAR(500),
    @min_score    INT = 65,
    @top_count    INT = 5,
    @outputCode   INT OUTPUT,
    @outputMsg    NVARCHAR(MAX) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SET @search_text = NULLIF(LTRIM(RTRIM(ISNULL(@search_text, ''))), '');
        SET @min_score = CASE
            WHEN ISNULL(@min_score, -1) < 0 THEN 0
            WHEN @min_score > 100 THEN 100
            ELSE @min_score
        END;
        SET @top_count = CASE
            WHEN ISNULL(@top_count, 0) < 1 THEN 1
            WHEN @top_count > 10 THEN 10
            ELSE @top_count
        END;

        IF @search_text IS NULL
        BEGIN
            SELECT TOP (0)
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
                dm.dm_chunk_count AS [dm_chunk_count],
                CAST(0 AS INT) AS [similarity_score]
            FROM [dbo].[document_mstr] dm WITH (NOLOCK);

            SET @outputCode = 1;
            SET @outputMsg = N'Success';
            RETURN;
        END;

        ;WITH ranked AS
        (
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
                dm.dm_chunk_count AS [dm_chunk_count],
                dbo.GetSimilarityScore(@search_text, dm.dm_name) AS [similarity_score]
            FROM [dbo].[document_mstr] dm WITH (NOLOCK)
        )
        SELECT TOP (@top_count)
            [dm_id],
            [dm_category],
            [dm_name],
            [dm_path],
            [dm_created_by],
            [dm_created_date],
            [dm_active],
            [dm_ingestion_status],
            [dm_ingested_at],
            [dm_ingestion_error],
            [dm_chunk_count],
            [similarity_score]
        FROM ranked
        WHERE [similarity_score] >= @min_score
        ORDER BY [similarity_score] DESC, [dm_created_date] DESC, [dm_id] DESC;

        SET @outputCode = 1;
        SET @outputMsg = N'Success';
    END TRY
    BEGIN CATCH
        SET @outputCode = -9;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
