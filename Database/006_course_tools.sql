ALTER TABLE assignment_questions ADD COLUMN question_type text NOT NULL DEFAULT 'essay'
 CHECK(question_type IN ('essay','true_false','single','multiple'));
ALTER TABLE assignment_questions ADD COLUMN options jsonb NOT NULL DEFAULT '[]';
ALTER TABLE assignment_questions ADD COLUMN standard_answer jsonb NOT NULL DEFAULT '[]';
CREATE TABLE course_resources (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), course_id uuid NOT NULL REFERENCES courses(id),
 file_name text NOT NULL, content bytea NOT NULL, uploaded_by uuid NOT NULL REFERENCES app_users(id),
 created_at timestamptz NOT NULL DEFAULT now(), download_count bigint NOT NULL DEFAULT 0
);
CREATE TABLE assignment_discussions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), assignment_id uuid NOT NULL REFERENCES assignments(id),
 author_id uuid NOT NULL REFERENCES app_users(id), question text NOT NULL, response text NOT NULL DEFAULT '',
 is_public boolean NOT NULL DEFAULT false, answered_by uuid REFERENCES app_users(id),
 created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE exams (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), course_id uuid NOT NULL REFERENCES courses(id),
 title text NOT NULL, instructions text NOT NULL DEFAULT '', status text NOT NULL DEFAULT 'draft' CHECK(status IN ('draft','published','closed')),
 opens_at timestamptz, due_at timestamptz, questions jsonb NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(), CHECK(due_at IS NULL OR opens_at IS NULL OR due_at > opens_at)
);
CREATE TABLE exam_attempts (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), exam_id uuid NOT NULL REFERENCES exams(id),
 student_id uuid NOT NULL REFERENCES app_users(id), answers jsonb NOT NULL DEFAULT '{}',
 submitted_at timestamptz, score numeric(10,2), feedback text NOT NULL DEFAULT '', graded_by uuid REFERENCES app_users(id),
 version integer NOT NULL DEFAULT 0, UNIQUE(exam_id,student_id)
);
CREATE INDEX ix_resources_course ON course_resources(course_id);
CREATE INDEX ix_discussions_assignment ON assignment_discussions(assignment_id);
CREATE INDEX ix_exams_course ON exams(course_id);
