-- Apply after EF migrations as the schema owner. Runtime logins are provisioned separately.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '3s';
SET LOCAL search_path = public, pg_catalog;

DO $roles$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'proofpath_api_runtime') THEN
        CREATE ROLE proofpath_api_runtime NOLOGIN NOSUPERUSER NOCREATEROLE NOCREATEDB NOBYPASSRLS;
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'proofpath_worker_runtime') THEN
        CREATE ROLE proofpath_worker_runtime NOLOGIN NOSUPERUSER NOCREATEROLE NOCREATEDB NOBYPASSRLS;
    END IF;
    IF EXISTS (
        SELECT FROM pg_roles
        WHERE rolname IN ('proofpath_api_runtime', 'proofpath_worker_runtime')
          AND (rolcanlogin OR rolsuper OR rolcreaterole OR rolcreatedb OR rolbypassrls)
    ) THEN
        RAISE EXCEPTION 'Unexpected privilege on ProofPath runtime roles';
    END IF;
END
$roles$;

GRANT USAGE ON SCHEMA public TO proofpath_api_runtime, proofpath_worker_runtime;
GRANT SELECT ON "Skills", "SkillAliases" TO proofpath_api_runtime, proofpath_worker_runtime;
GRANT SELECT ON "MatchingScoringVersions", "AspNetRoles", "AspNetRoleClaims" TO proofpath_api_runtime;
GRANT SELECT, INSERT ON "DataProtectionKeys" TO proofpath_api_runtime;
GRANT USAGE ON SEQUENCE "DataProtectionKeys_Id_seq", "AspNetUserClaims_Id_seq" TO proofpath_api_runtime;
GRANT SELECT, INSERT, UPDATE, DELETE ON "AspNetUsers", "AspNetUserClaims", "AspNetUserLogins",
    "AspNetUserRoles", "AspNetUserTokens" TO proofpath_api_runtime;

DO $policies$
DECLARE
    rule record;
    worker_rule record;
BEGIN
    FOR rule IN
        SELECT * FROM (VALUES
            ('CandidateProfiles', '"UserId" = nullif(current_setting(''proofpath.user_id'', true), '''')'),
            ('AnalysisJobs', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('BehavioralEvidenceItems', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('ConnectedAccounts', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('Credentials', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('Educations', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('EvidenceItems', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('Experiences', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('GitHubConnectionAttempts', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('GitHubInstallations', '"ConnectedAccountId" IN (SELECT "Id" FROM public."ConnectedAccounts")'),
            ('Jobs', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('JobRequirementExtractions', '"JobId" IN (SELECT "Id" FROM public."Jobs")'),
            ('JobRequirements', '"RequirementSetId" IN (SELECT "Id" FROM public."RequirementSets")'),
            ('JobTrackings', '"JobId" IN (SELECT "Id" FROM public."Jobs")'),
            ('MatchRecommendations', '"MatchResultId" IN (SELECT "Id" FROM public."MatchResults")'),
            ('MatchResults', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles") AND "JobId" IN (SELECT "Id" FROM public."Jobs")'),
            ('Projects', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('Repositories', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles") AND "ConnectedAccountId" IN (SELECT "Id" FROM public."ConnectedAccounts")'),
            ('RepositoryAnalyses', '"RepositoryId" IN (SELECT "Id" FROM public."Repositories")'),
            ('RequirementMatchEvidenceRecords', '"RequirementMatchRecordId" IN (SELECT "Id" FROM public."RequirementMatchRecords") AND "EvidenceItemId" IN (SELECT "Id" FROM public."EvidenceItems")'),
            ('RequirementMatchRecords', '"MatchResultId" IN (SELECT "Id" FROM public."MatchResults")'),
            ('RequirementSets', '"JobId" IN (SELECT "Id" FROM public."Jobs")'),
            ('Resumes', '"CandidateProfileId" IN (SELECT "Id" FROM public."CandidateProfiles")'),
            ('ResumeExtractions', '"ResumeId" IN (SELECT "Id" FROM public."Resumes")')
        ) AS policies(table_name, ownership)
    LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', rule.table_name);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON public.%I TO proofpath_api_runtime', rule.table_name);
        EXECUTE format('DROP POLICY IF EXISTS api_owner ON public.%I', rule.table_name);
        EXECUTE format(
            'CREATE POLICY api_owner ON public.%I TO proofpath_api_runtime USING (%s) WITH CHECK (%s)',
            rule.table_name, rule.ownership, rule.ownership);
    END LOOP;

    -- The Worker claims jobs across candidates. Only its supported operations receive SQL grants.
    FOR worker_rule IN
        SELECT * FROM (VALUES
            ('AnalysisJobs', 'SELECT, UPDATE'),
            ('ConnectedAccounts', 'SELECT'),
            ('GitHubInstallations', 'SELECT'),
            ('Repositories', 'SELECT, UPDATE'),
            ('RepositoryAnalyses', 'SELECT, INSERT'),
            ('EvidenceItems', 'SELECT, INSERT, UPDATE'),
            ('Projects', 'SELECT, INSERT'),
            ('Jobs', 'SELECT, UPDATE'),
            ('JobRequirementExtractions', 'SELECT, INSERT'),
            ('Resumes', 'SELECT, UPDATE'),
            ('ResumeExtractions', 'INSERT')
        ) AS permissions(table_name, operations)
    LOOP
        EXECUTE format('GRANT %s ON public.%I TO proofpath_worker_runtime', worker_rule.operations, worker_rule.table_name);
        EXECUTE format('DROP POLICY IF EXISTS worker_jobs ON public.%I', worker_rule.table_name);
        EXECUTE format('CREATE POLICY worker_jobs ON public.%I TO proofpath_worker_runtime USING (true) WITH CHECK (true)', worker_rule.table_name);
    END LOOP;
END
$policies$;

-- Cleanup rows store opaque object keys and cannot be read by the API login.
GRANT INSERT ON "PrivateFileDeletions" TO proofpath_api_runtime;
GRANT SELECT, UPDATE, DELETE ON "PrivateFileDeletions" TO proofpath_worker_runtime;
COMMIT;
