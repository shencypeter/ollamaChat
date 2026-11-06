-- Additive P1 migration; existing assignments keep their one-submit policy.
ALTER TABLE assignments ADD COLUMN allow_revisions boolean NOT NULL DEFAULT false;
ALTER TABLE assignments ADD COLUMN max_score numeric(8,2) NOT NULL DEFAULT 100 CHECK(max_score > 0);
ALTER TABLE rubrics ALTER COLUMN question_id DROP NOT NULL;
ALTER TABLE rubrics ADD COLUMN course_id uuid REFERENCES courses(id);
ALTER TABLE rubrics ADD COLUMN family_id uuid NOT NULL DEFAULT gen_random_uuid();
ALTER TABLE assignments ADD COLUMN rubric_id uuid REFERENCES rubrics(id);
CREATE UNIQUE INDEX uq_course_rubric_version ON rubrics(family_id,version_no);
CREATE INDEX ix_rubrics_course ON rubrics(course_id);
CREATE TABLE submission_revisions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 submission_id uuid NOT NULL REFERENCES submissions(id) ON DELETE CASCADE,
 revision_no integer NOT NULL CHECK(revision_no > 0),
 author_user_id uuid NOT NULL REFERENCES app_users(id),
 submitted_at timestamptz NOT NULL DEFAULT now(),
 answers jsonb NOT NULL CHECK(jsonb_typeof(answers)='array'),
 UNIQUE(submission_id,revision_no)
);
-- Backfill the last P0 submission without changing any existing answer history.
INSERT INTO submission_revisions(submission_id,revision_no,author_user_id,submitted_at,answers)
SELECT s.id,1,s.student_user_id,s.submitted_at,
 jsonb_agg(jsonb_build_object('questionId',sa.question_id,'revisionId',r.id,'text',r.answer_text) ORDER BY q.sort_order)
FROM submissions s JOIN submission_answers sa ON sa.submission_id=s.id
JOIN assignment_questions q ON q.id=sa.question_id JOIN answer_revisions r ON r.id=sa.current_revision_id
WHERE s.status='submitted' AND s.student_user_id IS NOT NULL
GROUP BY s.id;
ALTER TABLE grades ALTER COLUMN answer_revision_id DROP NOT NULL;
ALTER TABLE grades ADD COLUMN submission_revision_id uuid REFERENCES submission_revisions(id);
ALTER TABLE grades ADD COLUMN released_at timestamptz;
ALTER TABLE grades ADD COLUMN rubric_scores jsonb NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE grades ADD CONSTRAINT grade_has_subject CHECK(answer_revision_id IS NOT NULL OR submission_revision_id IS NOT NULL);
CREATE INDEX ix_grade_submission_revision ON grades(submission_revision_id,graded_at DESC);
-- Snapshots cannot be rewritten; deletion remains possible for scoped QA cleanup.
CREATE FUNCTION prevent_submission_revision_update() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Submitted snapshots are immutable'; END $$;
CREATE TRIGGER immutable_submission_revision BEFORE UPDATE ON submission_revisions
FOR EACH ROW EXECUTE FUNCTION prevent_submission_revision_update();
