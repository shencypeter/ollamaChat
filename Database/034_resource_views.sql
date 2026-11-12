CREATE TABLE resource_views (
 id uuid PRIMARY KEY,
 resource_id uuid NOT NULL REFERENCES course_resources(id) ON DELETE CASCADE,
 user_id uuid NOT NULL REFERENCES app_users(id),
 viewed_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_resource_views_resource_user ON resource_views(resource_id,user_id,viewed_at DESC);
