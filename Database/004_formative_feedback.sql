-- Reuse evaluation_runs; no synthetic rubric or separate evaluation store.
ALTER TABLE evaluation_runs ALTER COLUMN rubric_id DROP NOT NULL;
ALTER TABLE evaluation_runs ADD COLUMN purpose text NOT NULL DEFAULT 'legacy' CHECK(purpose IN ('legacy','formative'));
ALTER TABLE evaluation_runs ADD COLUMN requested_by_user_id uuid REFERENCES app_users(id);
ALTER TABLE evaluation_runs ADD COLUMN model_profile text;
ALTER TABLE evaluation_runs ADD COLUMN answer_sha256 text;
ALTER TABLE evaluation_runs ADD CONSTRAINT formative_run_identity CHECK(purpose<>'formative' OR
 (requested_by_user_id IS NOT NULL AND model_profile IS NOT NULL AND answer_sha256 IS NOT NULL));
CREATE INDEX ix_formative_revision_owner ON evaluation_runs(requested_by_user_id,answer_revision_id,created_at DESC) WHERE purpose='formative';
