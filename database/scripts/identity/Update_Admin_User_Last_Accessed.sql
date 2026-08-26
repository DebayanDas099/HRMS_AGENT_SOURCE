/*
    Updates admin user last accessed timestamp after successful login.
    Tables:
      [identity].[user_profile]
    Run against APPDB (or target HRMS SQL database).
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'identity')
    EXEC(N'CREATE SCHEMA [identity]');
GO

IF OBJECT_ID(N'[identity].[Update_Admin_User_Last_Accessed]', N'P') IS NOT NULL
    DROP PROCEDURE [identity].[Update_Admin_User_Last_Accessed];
GO

CREATE PROCEDURE [identity].[Update_Admin_User_Last_Accessed]
(
    @user_id    VARCHAR(20),
    @outputCode INT            OUTPUT,
    @outputMsg  NVARCHAR(MAX)  OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SET @user_id = NULLIF(LTRIM(RTRIM(ISNULL(@user_id, ''))), '');

        IF @user_id IS NULL
        BEGIN
            SET @outputCode = -1;
            SET @outputMsg = N'User id is required.';
            RETURN;
        END

        IF NOT EXISTS (
            SELECT 1
            FROM [identity].[user_profile] WITH (NOLOCK)
            WHERE usp_user_id = @user_id
              AND ISNULL(active, 'Y') = 'Y'
              AND deleted_date IS NULL
        )
        BEGIN
            SET @outputCode = -2;
            SET @outputMsg = N'Active user profile not found.';
            RETURN;
        END

        UPDATE [identity].[user_profile]
        SET usp_last_accessed_date = GETDATE(),
            modified_date = GETDATE(),
            modified_user = @user_id
        WHERE usp_user_id = @user_id
          AND ISNULL(active, 'Y') = 'Y'
          AND deleted_date IS NULL;

        SET @outputCode = 1;
        SET @outputMsg = N'Last accessed date updated successfully.';
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
