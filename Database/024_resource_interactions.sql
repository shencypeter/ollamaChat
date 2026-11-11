CREATE TABLE resource_likes (
 resource_id uuid NOT NULL REFERENCES course_resources(id) ON DELETE CASCADE,
 user_id uuid NOT NULL REFERENCES app_users(id),
 created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(resource_id,user_id)
);
CREATE TABLE resource_comments (
 id uuid PRIMARY KEY,
 resource_id uuid NOT NULL REFERENCES course_resources(id) ON DELETE CASCADE,
 author_id uuid NOT NULL REFERENCES app_users(id),
 body text NOT NULL CHECK(length(btrim(body)) BETWEEN 1 AND 4000),
 created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_resource_comments_page ON resource_comments(resource_id,created_at,id);
CREATE TABLE course_questions (
 id uuid PRIMARY KEY,
 course_id uuid NOT NULL REFERENCES courses(id),
 author_id uuid NOT NULL REFERENCES app_users(id),
 question text NOT NULL CHECK(length(btrim(question)) BETWEEN 1 AND 8000),
 response text NOT NULL DEFAULT '',
 answered_by uuid REFERENCES app_users(id),
 answered_at timestamptz,
 created_at timestamptz NOT NULL DEFAULT now(),
 version integer NOT NULL DEFAULT 1
);
CREATE INDEX ix_course_questions_page ON course_questions(course_id,created_at,id);
