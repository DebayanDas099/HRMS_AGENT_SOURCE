/*
    Admin login validation for HRMS Chatbot admin panel.
    Tables:
      [identity].[user_profile]
      [identity].[user_group]
    Run against APPDB (or target HRMS SQL database).
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'identity')
    EXEC(N'CREATE SCHEMA [identity]');
GO

IF OBJECT_ID(N'[identity].[Validate_Admin_Login]', N'P') IS NOT NULL
    DROP PROCEDURE [identity].[Validate_Admin_Login];
GO

CREATE PROCEDURE [identity].[Validate_Admin_Login]
(
    @userLoginName  VARCHAR(20),
    @password       VARCHAR(100),
    @mobile         VARCHAR(10) = NULL,
    @deviceId       VARCHAR(100) = NULL,
    @outputCode     INT            OUTPUT,
    @outputMsg      NVARCHAR(MAX)  OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SET @userLoginName = NULLIF(LTRIM(RTRIM(ISNULL(@userLoginName, ''))), '');
        SET @password = NULLIF(LTRIM(RTRIM(ISNULL(@password, ''))), '');
        SET @mobile = NULLIF(LTRIM(RTRIM(ISNULL(@mobile, ''))), '');
        SET @deviceId = NULLIF(LTRIM(RTRIM(ISNULL(@deviceId, ''))), '');

        IF @userLoginName IS NULL
        BEGIN
            SET @outputCode = -1;
            SET @outputMsg = N'User ID is required.';
            RETURN;
        END

        IF @password IS NULL
        BEGIN
            SET @outputCode = -2;
            SET @outputMsg = N'Password is required.';
            RETURN;
        END

        IF NOT EXISTS (
            SELECT 1
            FROM [identity].[user_profile] u WITH (NOLOCK)
            WHERE u.deleted_date IS NULL
              AND (
                    u.usp_user_id = @userLoginName
                    OR (@mobile IS NOT NULL AND u.usp_mobile = @mobile)
                  )
              AND u.usp_pswd = @password
        )
        BEGIN
            SET @outputCode = -3;
            SET @outputMsg = N'Invalid user ID or password.';
            RETURN;
        END

        IF NOT EXISTS (
            SELECT 1
            FROM [identity].[user_profile] u WITH (NOLOCK)
            WHERE u.deleted_date IS NULL
              AND (
                    u.usp_user_id = @userLoginName
                    OR (@mobile IS NOT NULL AND u.usp_mobile = @mobile)
                  )
              AND u.usp_pswd = @password
              AND ISNULL(u.active, 'Y') = 'Y'
        )
        BEGIN
            SET @outputCode = -4;
            SET @outputMsg = N'Your account is inactive. Please contact the administrator.';
            RETURN;
        END

        IF NOT EXISTS (
            SELECT 1
            FROM [identity].[user_profile] u WITH (NOLOCK)
            WHERE u.deleted_date IS NULL
              AND (
                    u.usp_user_id = @userLoginName
                    OR (@mobile IS NOT NULL AND u.usp_mobile = @mobile)
                  )
              AND u.usp_pswd = @password
              AND ISNULL(u.active, 'Y') = 'Y'
              AND ISNULL(u.usp_admin_yn, 'N') = 'Y'
        )
        BEGIN
            SET @outputCode = -5;
            SET @outputMsg = N'You do not have admin access to this panel.';
            RETURN;
        END

        IF EXISTS (
            SELECT 1
            FROM [identity].[user_profile] u WITH (NOLOCK)
            WHERE u.deleted_date IS NULL
              AND (
                    u.usp_user_id = @userLoginName
                    OR (@mobile IS NOT NULL AND u.usp_mobile = @mobile)
                  )
              AND u.usp_pswd = @password
              AND u.usp_exit_date IS NOT NULL
              AND CONVERT(DATE, u.usp_exit_date) <= CONVERT(DATE, GETDATE())
        )
        BEGIN
            SET @outputCode = -6;
            SET @outputMsg = N'Your account access has expired.';
            RETURN;
        END

        SELECT
            u.usp_user_id AS [user_id],
            u.usp_first_name AS [first_name],
            u.usp_last_name AS [last_name],
            LTRIM(RTRIM(ISNULL(u.usp_first_name, N'') + N' ' + ISNULL(u.usp_last_name, N''))) AS [full_name],
            u.usp_group_code AS [group_code],
            g.grp_user_group_desc AS [group_desc],
            u.usp_desig AS [designation],
            u.usp_dept AS [department],
            u.usp_depot AS [depot_code],
            u.usp_dept AS [depot_name],
            u.usp_mailid AS [mail_id],
            u.usp_mobile AS [mobile],
            u.usp_employee_id AS [employee_id],
            u.active AS [active],
            u.usp_admin_yn AS [usp_admin_yn],
            u.usp_admin_yn AS [admin_yn],
            u.usp_exit_date AS [last_working_date],
            N'N' AS [device_registered_yn],
            N'N' AS [impersonification_yn],
            NULL AS [profile_pic],
            NULL AS [img],
            u.usp_reporting_desig AS [usp_reporting_desig],
            u.usp_reporting_head AS [usp_reporting_head],
            u.usp_reporting_mailid AS [usp_reporting_mailid],
            u.usp_payroll AS [payroll],
            N'N' AS [resigned_yn],
            N'N' AS [single_terr_yn]
        FROM [identity].[user_profile] u WITH (NOLOCK)
        LEFT JOIN [identity].[user_group] g WITH (NOLOCK)
            ON g.grp_user_group_code = u.usp_group_code
           AND g.deleted_date IS NULL
        WHERE u.deleted_date IS NULL
          AND (
                u.usp_user_id = @userLoginName
                OR (@mobile IS NOT NULL AND u.usp_mobile = @mobile)
              )
          AND u.usp_pswd = @password
          AND ISNULL(u.active, 'Y') = 'Y'
          AND ISNULL(u.usp_admin_yn, 'N') = 'Y';

        SET @outputCode = 1;
        SET @outputMsg = N'Login validated successfully.';
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
