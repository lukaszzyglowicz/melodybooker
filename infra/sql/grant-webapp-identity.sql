-- One-time grant: create the Web App's system-assigned managed identity as a
-- contained database user inside melodybookerdb and grant it read/write roles.
--
-- Why manual: the SQL server is AAD-only (see infra/modules/sqlDatabase.bicep),
-- so only AAD-authenticated identities can connect at all, and only the AAD
-- admin can run CREATE USER ... FROM EXTERNAL PROVIDER. There is no CI-side
-- Azure identity at all in this project (Azure AD app registration is blocked
-- for this tenant — see plan.md Overview), so this can only ever be run by a
-- human AAD admin, never from a pipeline.
--
-- Run once, connected as the AAD admin (see infra/main.parameters.dev.json
-- aadAdminLogin), e.g. via Azure Data Studio AAD auth or:
--   sqlcmd -S melodybooker-dev-sql-xfpdpvqfykpgg.database.windows.net \
--     -d melodybookerdb -G -U lukasz.zyglowicz@hitachienergy.com \
--     -i infra/sql/grant-webapp-identity.sql

CREATE USER [melodybooker-dev-app-k7sqmyvh2szbg] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [melodybooker-dev-app-k7sqmyvh2szbg];
ALTER ROLE db_datawriter ADD MEMBER [melodybooker-dev-app-k7sqmyvh2szbg];
