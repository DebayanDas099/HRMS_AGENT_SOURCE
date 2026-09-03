/*
    Records an approve/reject decision against a leave application.
    Consumed by ILeaveRepo.UpdateLeaveApplicationStatusAsync (LeaveRepo.cs) - the four
    input parameter names below must match exactly, they are bound positionally by
    name from LeaveRepo.cs.

    uld_is_approve is the authoritative pending/approved/rejected flag (NULL = pending,
    used by Get_Pending_Leave_Applications); uld_leave_status carries the same decision
    as readable text for anything that displays it. Both are set together so they never
    drift apart.
*/
CREATE OR ALTER PROCEDURE [dbo].[Update_Leave_Application_Status]
    @application_reference VARCHAR(20),
    @new_status             VARCHAR(20),
    @approved_by            VARCHAR(50),
    @note                   VARCHAR(500) = NULL,
    @outputCode             INT OUTPUT,
    @outputMsg              VARCHAR(MAX) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF @new_status NOT IN ('Approved', 'Rejected')
        BEGIN
            SET @outputCode = -1;
            SET @outputMsg = 'Invalid status: must be Approved or Rejected.';
            RETURN;
        END

        DECLARE @id BIGINT = TRY_CAST(@application_reference AS BIGINT);

        IF @id IS NULL OR NOT EXISTS (
            SELECT 1 FROM dbo.user_leave_details
            WHERE uld_id = @id AND uld_is_approve IS NULL AND ISNULL(uld_active, 1) = 1
        )
        BEGIN
            SET @outputCode = -1;
            SET @outputMsg = 'Application not found or no longer pending.';
            RETURN;
        END

        UPDATE dbo.user_leave_details
        SET
            uld_is_approve      = CASE WHEN @new_status = 'Approved' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END,
            uld_leave_status    = @new_status,
            uld_leave_status_on = CAST(GETDATE() AS DATE),
            uld_reason          = @note,
            uld_approved_by     = @approved_by,
            uld_approved_on     = GETDATE(),
            uld_modified_date   = GETDATE(),
            uld_modified_user   = @approved_by
        WHERE uld_id = @id;

        SET @outputCode = 1;
        SET @outputMsg = 'Application ' + @new_status + ' successfully.';
    END TRY
    BEGIN CATCH
        SET @outputCode = -1;
        SET @outputMsg = ERROR_MESSAGE();
    END CATCH
END
GO
