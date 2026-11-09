CREATE TABLE course_showcase_publications (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),showcase_id uuid NOT NULL,
 revision integer NOT NULL,approved_by uuid NOT NULL REFERENCES app_users(id),approved_at timestamptz NOT NULL DEFAULT now(),
 authorization_reference text NOT NULL CHECK(length(trim(authorization_reference)) BETWEEN 1 AND 500),
 anonymized boolean NOT NULL,authorized boolean NOT NULL,no_answer_leak boolean NOT NULL,
 CHECK(anonymized AND authorized AND no_answer_leak),
 FOREIGN KEY(showcase_id,revision) REFERENCES course_showcase_versions(showcase_id,revision) ON DELETE CASCADE
);
CREATE INDEX ix_showcase_publications ON course_showcase_publications(showcase_id,approved_at);
CREATE FUNCTION prevent_showcase_record_update() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Showcase snapshots and publication approvals are immutable'; END $$;
CREATE TRIGGER immutable_showcase_version BEFORE UPDATE ON course_showcase_versions
 FOR EACH ROW EXECUTE FUNCTION prevent_showcase_record_update();
CREATE TRIGGER immutable_showcase_publication BEFORE UPDATE ON course_showcase_publications
 FOR EACH ROW EXECUTE FUNCTION prevent_showcase_record_update();
