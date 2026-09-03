CREATE OR ALTER PROCEDURE [dbo].[Get_User_Email_By_Mobile]
(
    @mobile VARCHAR(20)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        usp_mailid
    FROM [dbo].[user_profile]
    WHERE usp_mobile = @mobile
      AND ISNULL(active, 'N') = 'Y'
      AND ISNULL(usp_mailid, '') <> '';
END
