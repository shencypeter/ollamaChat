CREATE TABLE studio_profiles (
 user_id uuid PRIMARY KEY REFERENCES app_users(id), department_year text NOT NULL DEFAULT '',
 completed_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE studio_cases (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), course_id uuid NOT NULL REFERENCES courses(id),
 active_version_id uuid, draft_version_id uuid, created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE studio_case_versions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), case_id uuid NOT NULL REFERENCES studio_cases(id),
 version_no integer NOT NULL, title text NOT NULL, material text NOT NULL,
 prompts jsonb NOT NULL, reference_points jsonb NOT NULL,
 created_by uuid NOT NULL REFERENCES app_users(id), created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(case_id,version_no), UNIQUE(id,case_id)
);
ALTER TABLE studio_cases ADD FOREIGN KEY(active_version_id,id) REFERENCES studio_case_versions(id,case_id);
ALTER TABLE studio_cases ADD FOREIGN KEY(draft_version_id,id) REFERENCES studio_case_versions(id,case_id);
CREATE TABLE studio_templates (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), version_no integer NOT NULL UNIQUE,
 body text NOT NULL, created_by uuid REFERENCES app_users(id), created_at timestamptz NOT NULL DEFAULT now()
);
INSERT INTO studio_templates(version_no,body) VALUES(1,'請用支持學生學習的繁體中文，指出有案例證據支持的分析、需要修正的推論，以及可採取的下一步。以提問引導修訂，不提供代寫答案。');
CREATE TABLE studio_settings (
 id boolean PRIMARY KEY DEFAULT true CHECK(id), feedback_enabled boolean NOT NULL DEFAULT true,
 timeout_seconds integer NOT NULL DEFAULT 180 CHECK(timeout_seconds BETWEEN 5 AND 300),
 max_requests_per_revision integer NOT NULL DEFAULT 3 CHECK(max_requests_per_revision BETWEEN 1 AND 10),
 version integer NOT NULL DEFAULT 1, updated_by uuid REFERENCES app_users(id), updated_at timestamptz NOT NULL DEFAULT now()
);
INSERT INTO studio_settings(id) VALUES(true);
CREATE TABLE studio_attempts (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), case_version_id uuid NOT NULL REFERENCES studio_case_versions(id),
 student_id uuid NOT NULL REFERENCES app_users(id), is_practice boolean NOT NULL DEFAULT false,
 version integer NOT NULL DEFAULT 0, submitted_at timestamptz,
 UNIQUE(case_version_id,student_id,is_practice)
);
CREATE TABLE studio_answer_versions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), attempt_id uuid NOT NULL REFERENCES studio_attempts(id),
 stage text NOT NULL CHECK(stage IN ('premorbid','illnessCourse','functionalAssessment','psychosocial')),
 revision_no integer NOT NULL, answer_text text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(attempt_id,stage,revision_no)
);
CREATE TABLE studio_feedback_runs (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), answer_version_id uuid NOT NULL REFERENCES studio_answer_versions(id),
 template_id uuid NOT NULL REFERENCES studio_templates(id), model_name text NOT NULL, model_profile text NOT NULL,
 prompt_version text NOT NULL, status text NOT NULL DEFAULT 'pending' CHECK(status IN ('pending','completed','failed')),
 findings jsonb, error_code text, created_at timestamptz NOT NULL DEFAULT now(), completed_at timestamptz
);
CREATE UNIQUE INDEX uq_studio_pending ON studio_feedback_runs(answer_version_id) WHERE status='pending';
CREATE INDEX ix_studio_cases_course ON studio_cases(course_id);
CREATE INDEX ix_studio_attempt_student ON studio_attempts(student_id);
CREATE INDEX ix_studio_answers_attempt ON studio_answer_versions(attempt_id,stage,revision_no DESC);
