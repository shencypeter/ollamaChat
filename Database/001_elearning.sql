-- User-supplied NSYSU e-learning schema. Applied by the transactional migration runner.
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE TABLE app_users (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), identity_provider text, external_subject text,
 student_number text, display_name text NOT NULL, email text,
 status text NOT NULL DEFAULT 'active' CHECK (status IN ('active','disabled')),
 created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(identity_provider, external_subject), UNIQUE(student_number)
);
CREATE TABLE courses (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL,
 title text NOT NULL, term_code text NOT NULL, owner_user_id uuid NOT NULL REFERENCES app_users(id),
 status text NOT NULL DEFAULT 'draft' CHECK(status IN ('draft','active','archived')),
 created_at timestamptz NOT NULL DEFAULT now(), UNIQUE(code,term_code)
);
CREATE TABLE course_members (
 course_id uuid NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
 user_id uuid NOT NULL REFERENCES app_users(id),
 course_role text NOT NULL CHECK(course_role IN ('teacher','ta','student')),
 status text NOT NULL DEFAULT 'active' CHECK(status IN ('active','inactive')),
 joined_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(course_id,user_id)
);
CREATE TABLE course_groups (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), course_id uuid NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
 name text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(), UNIQUE(course_id,name), UNIQUE(id,course_id)
);
CREATE TABLE course_group_members (
 group_id uuid NOT NULL REFERENCES course_groups(id) ON DELETE CASCADE,
 user_id uuid NOT NULL REFERENCES app_users(id), joined_at timestamptz NOT NULL DEFAULT now(), left_at timestamptz,
 PRIMARY KEY(group_id,user_id,joined_at), CHECK(left_at IS NULL OR left_at >= joined_at)
);
CREATE TABLE assignments (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), course_id uuid NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
 title text NOT NULL, instructions text NOT NULL DEFAULT '',
 submission_mode text NOT NULL DEFAULT 'individual' CHECK(submission_mode IN ('individual','group')),
 status text NOT NULL DEFAULT 'draft' CHECK(status IN ('draft','published','closed')),
 opens_at timestamptz, due_at timestamptz, created_at timestamptz NOT NULL DEFAULT now(),
 CHECK(due_at IS NULL OR opens_at IS NULL OR due_at >= opens_at), UNIQUE(id,course_id)
);
CREATE TABLE assignment_questions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), assignment_id uuid NOT NULL REFERENCES assignments(id) ON DELETE CASCADE,
 stage_code text, prompt_text text NOT NULL, sort_order integer NOT NULL CHECK(sort_order > 0),
 max_score numeric(8,2), CHECK(max_score IS NULL OR max_score > 0), UNIQUE(assignment_id,sort_order), UNIQUE(id,assignment_id)
);
CREATE TABLE submissions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), assignment_id uuid NOT NULL REFERENCES assignments(id),
 student_user_id uuid REFERENCES app_users(id), course_group_id uuid REFERENCES course_groups(id),
 attempt_no integer NOT NULL CHECK(attempt_no > 0), status text NOT NULL DEFAULT 'draft' CHECK(status IN ('draft','submitted','withdrawn')),
 created_at timestamptz NOT NULL DEFAULT now(), submitted_at timestamptz,
 CHECK ((student_user_id IS NOT NULL) <> (course_group_id IS NOT NULL)),
 CHECK(status <> 'submitted' OR submitted_at IS NOT NULL), UNIQUE(id,assignment_id)
);
CREATE UNIQUE INDEX uq_submission_individual_attempt ON submissions(assignment_id,student_user_id,attempt_no) WHERE student_user_id IS NOT NULL;
CREATE UNIQUE INDEX uq_submission_group_attempt ON submissions(assignment_id,course_group_id,attempt_no) WHERE course_group_id IS NOT NULL;
CREATE TABLE submission_answers (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), submission_id uuid NOT NULL, assignment_id uuid NOT NULL,
 question_id uuid NOT NULL, current_revision_id uuid,
 FOREIGN KEY(submission_id,assignment_id) REFERENCES submissions(id,assignment_id),
 FOREIGN KEY(question_id,assignment_id) REFERENCES assignment_questions(id,assignment_id),
 UNIQUE(submission_id,question_id), UNIQUE(id,submission_id)
);
CREATE TABLE answer_revisions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), answer_id uuid NOT NULL REFERENCES submission_answers(id),
 revision_no integer NOT NULL CHECK(revision_no > 0), answer_text text NOT NULL,
 created_by_user_id uuid NOT NULL REFERENCES app_users(id), created_at timestamptz NOT NULL DEFAULT now(), is_submitted boolean NOT NULL DEFAULT false,
 UNIQUE(answer_id,revision_no), UNIQUE(id,answer_id)
);
ALTER TABLE submission_answers ADD CONSTRAINT fk_current_revision_same_answer
 FOREIGN KEY(current_revision_id,id) REFERENCES answer_revisions(id,answer_id) DEFERRABLE INITIALLY DEFERRED;
CREATE TABLE rubrics (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), question_id uuid NOT NULL REFERENCES assignment_questions(id),
 version_no integer NOT NULL CHECK(version_no > 0), title text NOT NULL,
 status text NOT NULL DEFAULT 'draft' CHECK(status IN ('draft','published','retired')),
 created_by_user_id uuid NOT NULL REFERENCES app_users(id), created_at timestamptz NOT NULL DEFAULT now(), UNIQUE(question_id,version_no)
);
CREATE TABLE rubric_criteria (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), rubric_id uuid NOT NULL REFERENCES rubrics(id) ON DELETE CASCADE,
 criterion_code text NOT NULL, title text NOT NULL, description text NOT NULL DEFAULT '',
 weight numeric(8,3) NOT NULL DEFAULT 1 CHECK(weight > 0), sort_order integer NOT NULL CHECK(sort_order > 0),
 UNIQUE(rubric_id,criterion_code), UNIQUE(rubric_id,sort_order)
);
CREATE TABLE rubric_levels (
 criterion_id uuid NOT NULL REFERENCES rubric_criteria(id) ON DELETE CASCADE,
 level_no smallint NOT NULL CHECK(level_no BETWEEN 1 AND 4), description text NOT NULL, PRIMARY KEY(criterion_id,level_no)
);
CREATE TABLE evaluation_runs (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), answer_revision_id uuid NOT NULL REFERENCES answer_revisions(id),
 rubric_id uuid NOT NULL REFERENCES rubrics(id), model_name text NOT NULL, prompt_version text NOT NULL,
 model_parameters jsonb NOT NULL DEFAULT '{}'::jsonb,
 status text NOT NULL DEFAULT 'pending' CHECK(status IN ('pending','completed','failed')),
 score numeric(8,2), findings jsonb, feedback_markdown text,
 error_message text, created_at timestamptz NOT NULL DEFAULT now(), completed_at timestamptz,
 CHECK(score IS NULL OR score BETWEEN 1 AND 4),
 CHECK(status <> 'completed' OR (score IS NOT NULL AND findings IS NOT NULL AND completed_at IS NOT NULL))
);
CREATE TABLE grades (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), answer_revision_id uuid NOT NULL REFERENCES answer_revisions(id),
 grader_user_id uuid NOT NULL REFERENCES app_users(id), rubric_id uuid REFERENCES rubrics(id),
 score numeric(8,2) NOT NULL CHECK(score >= 0), feedback text NOT NULL DEFAULT '',
 graded_at timestamptz NOT NULL DEFAULT now(), supersedes_grade_id uuid REFERENCES grades(id)
);
CREATE TABLE audit_events (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, actor_user_id uuid REFERENCES app_users(id),
 entity_type text NOT NULL, entity_id uuid NOT NULL, action text NOT NULL, metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
 occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_members_user ON course_members(user_id);
CREATE INDEX ix_assignments_course ON assignments(course_id);
CREATE INDEX ix_questions_assignment ON assignment_questions(assignment_id);
CREATE INDEX ix_submissions_assignment ON submissions(assignment_id);
CREATE INDEX ix_answers_submission ON submission_answers(submission_id);
CREATE INDEX ix_revisions_answer ON answer_revisions(answer_id,revision_no DESC);
CREATE INDEX ix_evaluations_revision ON evaluation_runs(answer_revision_id,created_at DESC);
CREATE INDEX ix_grades_revision ON grades(answer_revision_id,graded_at DESC);
