-- Domain users stay in app_users; local login credentials are a separate concern.
-- Platform permissions are distinct from course_members.course_role.
CREATE TABLE portal_credentials (
 user_id uuid PRIMARY KEY REFERENCES app_users(id),
 normalized_email text NOT NULL UNIQUE,
 password_hash text NOT NULL,
 platform_role text NOT NULL CHECK(platform_role IN ('Professor','TeachingAssistant','Student')),
 failed_login_count integer NOT NULL DEFAULT 0,
 locked_until timestamptz,
 created_at timestamptz NOT NULL DEFAULT now()
);
