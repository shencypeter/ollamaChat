-- Additive learning-time tracking. Resource bytes are currently immutable: an upload ID
-- is its content version; renaming never resets evidence.
CREATE TABLE quiz_learning_requirements (
 assignment_id uuid NOT NULL REFERENCES assignments(id),
 resource_id uuid NOT NULL REFERENCES course_resources(id),
 minimum_seconds integer NOT NULL CHECK(minimum_seconds BETWEEN 1 AND 86400),
 PRIMARY KEY(assignment_id,resource_id)
);
CREATE TABLE learning_time_sessions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 resource_id uuid NOT NULL REFERENCES course_resources(id),
 user_id uuid NOT NULL REFERENCES app_users(id),
 is_practice boolean NOT NULL,
 started_at timestamptz NOT NULL,
 last_seen_at timestamptz NOT NULL,
 last_position double precision,
 credited_milliseconds bigint NOT NULL DEFAULT 0 CHECK(credited_milliseconds>=0),
 closed boolean NOT NULL DEFAULT false
);
-- At most one active resource across all tabs/devices/namespaces per person.
CREATE UNIQUE INDEX uq_learning_active_user ON learning_time_sessions(user_id) WHERE NOT closed;
CREATE INDEX ix_learning_resource_user ON learning_time_sessions(resource_id,user_id,is_practice);
CREATE TABLE quiz_learning_starts (
 assignment_id uuid NOT NULL REFERENCES assignments(id),
 user_id uuid NOT NULL REFERENCES app_users(id),
 is_practice boolean NOT NULL,
 started_at timestamptz NOT NULL,
 evidence jsonb NOT NULL,
 PRIMARY KEY(assignment_id,user_id,is_practice)
);
