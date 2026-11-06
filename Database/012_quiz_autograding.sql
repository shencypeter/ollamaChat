CREATE TABLE quiz_grading (
 snapshot_id uuid PRIMARY KEY REFERENCES submission_revisions(id) ON DELETE CASCADE,
 findings jsonb NOT NULL CHECK(jsonb_typeof(findings)='array'),
 created_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE grades ALTER COLUMN grader_user_id DROP NOT NULL;
ALTER TABLE grades ADD COLUMN essay_scores jsonb NOT NULL DEFAULT '{}'::jsonb;
