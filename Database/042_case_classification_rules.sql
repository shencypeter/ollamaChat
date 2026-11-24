-- Append-only course rule revisions. Codes are disabled instead of removed.
CREATE TABLE case_classification_rule_versions (
 course_id uuid NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
 version integer NOT NULL CHECK(version>0),
 definitions jsonb NOT NULL CHECK(jsonb_typeof(definitions)='array' AND jsonb_array_length(definitions) BETWEEN 1 AND 128),
 updated_at timestamptz NOT NULL,
 updated_by uuid NOT NULL REFERENCES app_users(id),
 PRIMARY KEY(course_id,version)
);
CREATE FUNCTION protect_case_classification_rules() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Case classification rule versions are immutable';
END $$;
CREATE TRIGGER case_classification_rules_immutable BEFORE UPDATE ON case_classification_rule_versions
 FOR EACH ROW EXECUTE FUNCTION protect_case_classification_rules();

-- Null metadata denotes legacy static-catalog messages. Never backfill/reinterpret.
ALTER TABLE case_analysis_messages
 ADD COLUMN rule_version integer,
 ADD COLUMN rule_snapshot jsonb,
 ADD COLUMN prompt_snapshot text,
 ADD CONSTRAINT case_chat_rule_snapshot CHECK (
  (rule_version IS NULL AND rule_snapshot IS NULL AND prompt_snapshot IS NULL)
  OR (rule_version IS NOT NULL AND rule_version>=0 AND rule_snapshot IS NOT NULL AND jsonb_typeof(rule_snapshot)='array'
   AND jsonb_array_length(rule_snapshot) BETWEEN 1 AND 128 AND prompt_snapshot IS NOT NULL)
 );
CREATE OR REPLACE FUNCTION protect_case_chat_content() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF (NEW.id,NEW.session_id,NEW.role,NEW.content,NEW.sequence_number,NEW.in_reply_to,NEW.model_name,NEW.prompt_version,NEW.created_at,NEW.revises_message_id,NEW.performance_metrics,NEW.annotations,NEW.rule_version,NEW.rule_snapshot,NEW.prompt_snapshot)
    IS DISTINCT FROM (OLD.id,OLD.session_id,OLD.role,OLD.content,OLD.sequence_number,OLD.in_reply_to,OLD.model_name,OLD.prompt_version,OLD.created_at,OLD.revises_message_id,OLD.performance_metrics,OLD.annotations,OLD.rule_version,OLD.rule_snapshot,OLD.prompt_snapshot) THEN
   RAISE EXCEPTION 'Case chat message content and metadata are immutable';
 END IF;
 RETURN NEW;
END $$;
