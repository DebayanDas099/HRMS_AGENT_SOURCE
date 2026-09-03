/*
    Lists active mobile numbers from user_profile, for the anonymous chat UI's
    mobile-number dropdown (HRMS_CHATBOT_SOURCE Chat area).

    "Active" here means: usp_mobile is populated AND the user has not exited
    (usp_exit_date IS NULL) - the same registered/enabled-number concept the
    chat access-control logic (IAgentAccessService) already relies on.

    Table: [identity].[user_profile]
    Run against APPDB (or target HRMS SQL database).
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'identity')
    EXEC(N'CREATE SCHEMA [identity]');
GO

IF OBJECT_ID(N'[dbo].[usp_GetActiveMobileNumbers]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[usp_GetActiveMobileNumbers];
GO

CREATE PROCEDURE [dbo].[usp_GetActiveMobileNumbers]
(
    @outputCode INT            OUTPUT,
    @outputMsg  NVARCHAR(MAX)  OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT DISTINCT
            LTRIM(RTRIM(u.usp_mobile)) AS [mobile_number]
        FROM [identity].[user_profile] u WITH (NOLOCK)
        WHERE u.deleted_date IS NULL
          AND u.usp_mobile IS NOT NULL
          AND LTRIM(RTRIM(u.usp_mobile)) <> ''
          AND u.usp_exit_date IS NULL
        ORDER BY [mobile_number];

        SET @outputCode = 1;
        SET @outputMsg = N'Active mobile numbers fetched successfully.';
    END TRY
    BEGIN CATCH
        SET @outputCode = -9;
        IF OBJECT_ID(N'[dbo].[Get_Error_Mesage]', N'FN') IS NOT NULL
            SET @outputMsg = [dbo].[Get_Error_Mesage](ERROR_LINE(), ERROR_MESSAGE());
        ELSE
            SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
