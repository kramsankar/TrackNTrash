/* =====================================================================================
   Migration 009 — CameraDevice role for unattended hardware.

   Dock cameras have to reach the API for two things: the manifest delta sync they use to
   know how many cartons a tray should hold, and their own heartbeat. Until now those two
   endpoints were open, so no account was needed. They are guarded like everything else,
   which means the cameras need an identity.

   A camera runs on a device nobody watches, mounted where a contractor can reach it, so
   its credentials are the likeliest in the system to leak. This role is therefore refused
   by the API's default policy and admitted only on the two endpoints above — a stolen
   camera credential reads no orders, creates no trips and moves no stock.

   The role deliberately gets no RoleFormMapping rows: it is not a console login and has no
   business opening a screen.

   The service account itself is created through POST /auth/users with the setup key, so no
   password is written into a migration file.

   Idempotent.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

MERGE ops.Role AS t
USING (SELECT 'CameraDevice' AS RoleName,
              'Unattended dock camera. Manifest sync and heartbeat only — never a console login.' AS Description,
              0 AS IsAdmin) AS s
ON t.RoleName = s.RoleName
WHEN NOT MATCHED THEN
    INSERT (RoleName, Description, IsAdmin) VALUES (s.RoleName, s.Description, s.IsAdmin);
GO

PRINT 'Migration 009 complete.';
GO
