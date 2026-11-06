-- Preserve existing platform capabilities while separating management from teaching.
ALTER TABLE portal_credentials ADD COLUMN can_create_courses boolean NOT NULL DEFAULT false;
ALTER TABLE portal_credentials DROP CONSTRAINT portal_credentials_platform_role_check;
INSERT INTO audit_events(actor_user_id,entity_type,entity_id,action,metadata)
 SELECT NULL,'app_users',user_id,'account.permissions.migrated',jsonb_build_object('previousRole',platform_role,'newRole',CASE WHEN platform_role='Professor' THEN 'Administrator' ELSE 'Member' END,'canCreateCourses',platform_role='Professor') FROM portal_credentials;
UPDATE portal_credentials SET can_create_courses=(platform_role='Professor'),platform_role=CASE WHEN platform_role='Professor' THEN 'Administrator' ELSE 'Member' END;
ALTER TABLE portal_credentials ADD CONSTRAINT portal_credentials_platform_role_check CHECK(platform_role IN ('Administrator','Member'));
