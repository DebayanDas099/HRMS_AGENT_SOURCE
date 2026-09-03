/*
    Leave balance summary for one employee (by mobile) over a date range.
    Consumed by ILeaveRepo.GetLeaveDetailsByUserAsync (LeaveRepo.cs) and mapped by
    LeaveAdapter.MapBalanceSummary - the output column names below are exactly what
    that mapping reads. The chatbot was showing all-zero balances because the deployed
    procedure's result set did not carry these names (accrued_leave_balance etc. were
    absent), so every value fell back to 0.

    Schema facts this is built on (from dbo.user_leave_balance data):
      - ulb_emp_id holds user_profile.usp_user_id values (e.g. 'SO124'), so the join
        is on usp_user_id - NOT usp_employee_id.
      - ulb_leave_month is 'yyyyMM'.
      - ulb_leave_balance = ulb_leave_credit + ulb_leave_adjust - ulb_leave_applied
        (holds for every existing row), so it is used directly as "remaining".

    Assumptions to verify:
      - loss_of_pay: no source column exists in user_leave_balance; returned as 0.
      - contract_* columns are derived from user_profile.usp_exit_date.
      - leave_type_ld_lov_id_fk: no source column; returned NULL (adapter maps to 0).
*/
CREATE OR ALTER PROCEDURE [dbo].[Get_Leave_Details_By_User]
    @mobile     VARCHAR(20),
    @start_date DATE,
    @end_date   DATE,
    @outputCode INT OUTPUT,
    @outputMsg  VARCHAR(MAX) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        DECLARE @emp_id   VARCHAR(20);
        DECLARE @exitDate DATETIME;

        SELECT TOP (1)
            @emp_id   = usp_user_id,
            @exitDate = usp_exit_date
        FROM dbo.user_profile
        WHERE usp_mobile = @mobile
          AND active = 'Y';

        IF @emp_id IS NULL
        BEGIN
            SET @outputCode = -1;
            SET @outputMsg  = 'Mobile number is not registered.';
            RETURN;
        END

        DECLARE @from_month VARCHAR(6) = FORMAT(@start_date, 'yyyyMM');
        DECLARE @to_month   VARCHAR(6) = FORMAT(@end_date,   'yyyyMM');

        SELECT
            @emp_id                                                          AS emp_id,
            ISNULL(SUM(ulb.ulb_leave_credit + ISNULL(ulb.ulb_leave_adjust, 0)), 0) AS accrued_leave_balance,
            ISNULL(SUM(ulb.ulb_leave_applied), 0)                            AS applied_leave,
            ISNULL(SUM(ulb.ulb_leave_balance), 0)                            AS remaining_leave_balance,
            CAST(0 AS INT)                                                   AS loss_of_pay,
            @exitDate                                                        AS contract_end_date,
            CASE WHEN @exitDate IS NULL THEN NULL
                 ELSE DATEDIFF(DAY, CAST(GETDATE() AS DATE), @exitDate) END  AS days_until_contract_end,
            CASE WHEN @exitDate IS NULL OR @exitDate > GETDATE() THEN 'Active'
                 ELSE 'Exited' END                                           AS contract_status,
            CAST(NULL AS BIGINT)                                             AS leave_type_ld_lov_id_fk
        FROM dbo.user_leave_balance ulb
        WHERE ulb.ulb_emp_id = @emp_id
          AND ulb.ulb_leave_month BETWEEN @from_month AND @to_month;

        SET @outputCode = 1;
        SET @outputMsg  = 'OK';
    END TRY
    BEGIN CATCH
        SET @outputCode = -1;
        SET @outputMsg  = ERROR_MESSAGE();
    END CATCH
END
GO
