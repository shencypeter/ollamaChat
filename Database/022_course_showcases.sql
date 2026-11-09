CREATE TABLE course_showcases (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),course_id uuid NOT NULL REFERENCES courses(id),
 source_snapshot_id uuid REFERENCES submission_revisions(id),source_question_id uuid REFERENCES assignment_questions(id),
 source_report_submission_id uuid REFERENCES report_submissions(id),
 status text NOT NULL DEFAULT 'draft' CHECK(status IN ('draft','published','withdrawn')),
 version integer NOT NULL DEFAULT 1 CHECK(version>0),draft_revision integer NOT NULL DEFAULT 1,
 published_revision integer,published_at timestamptz,withdrawn_at timestamptz,
 approved_by uuid REFERENCES app_users(id),authorization_reference text NOT NULL DEFAULT '',
 created_by uuid NOT NULL REFERENCES app_users(id),created_at timestamptz NOT NULL DEFAULT now(),
 CHECK((source_snapshot_id IS NOT NULL AND source_question_id IS NOT NULL AND source_report_submission_id IS NULL) OR
       (source_snapshot_id IS NULL AND source_question_id IS NULL AND source_report_submission_id IS NOT NULL)),
 CHECK(status<>'published' OR (published_revision IS NOT NULL AND published_at IS NOT NULL AND approved_by IS NOT NULL))
);
CREATE TABLE course_showcase_versions (
 showcase_id uuid NOT NULL REFERENCES course_showcases(id) ON DELETE CASCADE,revision integer NOT NULL CHECK(revision>0),
 title text NOT NULL CHECK(length(title) BETWEEN 1 AND 200),background text NOT NULL DEFAULT '' CHECK(length(background)<=24000),
 answer text NOT NULL DEFAULT '' CHECK(length(answer)<=64000),notes text NOT NULL DEFAULT '' CHECK(length(notes)<=16000),
 pdf bytea CHECK(pdf IS NULL OR octet_length(pdf) BETWEEN 1 AND 20971520),
 created_by uuid NOT NULL REFERENCES app_users(id),created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(showcase_id,revision)
);
CREATE INDEX ix_showcase_course_status ON course_showcases(course_id,status,created_at,id);
