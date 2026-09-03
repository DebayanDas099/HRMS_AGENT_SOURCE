/*
    Lists every leave application still awaiting an approve/reject decision.
    Consumed by ILeaveRepo.GetPendingLeaveApplicationsAsync (LeaveRepo.cs) and mapped
    by LeaveAdapter.MapPendingApplications - the six output columns below are exactly
    what that mapping reads, do not rename them without updating both.

    Schema assumption to verify: uld_emp_id is joined against usp_employee_id, not
    usp_user_id. If this returns zero rows against real data, that join is the first
    thing to check.
*/
CREATE OR ALTER PROCEDURE [dbo].[Get_Pending_Leave_Applications]
    @outputCode INT OUTPUT,
    @outputMsg  VARCHAR(MAX) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT
            CAST(uld.uld_id AS VARCHAR(20))                                   AS application_reference,
            up.usp_mobile                                                     AS mobile,
            LTRIM(RTRIM(up.usp_first_name + ' ' + ISNULL(up.usp_last_name, ''))) AS employee_name,
            uld.uld_leave_from_date                                           AS from_date,
            uld.uld_leave_to_date                                             AS to_date,
            uld.uld_leave_reason                                              AS reason,
            uld.uld_leaved_applied_on                                         AS applied_on
        FROM dbo.user_leave_details uld
        INNER JOIN dbo.user_profile up ON up.usp_employee_id = uld.uld_emp_id
        WHERE uld.uld_is_approve IS NULL
          AND ISNULL(uld.uld_active, 1) = 1
        ORDER BY uld.uld_leaved_applied_on ASC;

        SET @outputCode = 1;
        SET @outputMsg = 'OK';
    END TRY
    BEGIN CATCH
        SET @outputCode = -1;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
