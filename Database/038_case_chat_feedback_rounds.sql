ALTER TABLE case_analysis_messages
 ADD COLUMN performance_metrics jsonb,
 ADD COLUMN revises_message_id uuid REFERENCES case_analysis_messages(id),
 ADD CONSTRAINT case_chat_performance_role CHECK(performance_metrics IS NULL OR (role='assistant' AND jsonb_typeof(performance_metrics)='object')),
 ADD CONSTRAINT case_chat_revision_role CHECK(revises_message_id IS NULL OR (role='user' AND revises_message_id<>id));
CREATE INDEX ix_case_chat_revision_parent ON case_analysis_messages(revises_message_id) WHERE revises_message_id IS NOT NULL;

CREATE FUNCTION validate_case_chat_revision_parent() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.revises_message_id IS NOT NULL AND NOT EXISTS(
   SELECT 1 FROM case_analysis_messages parent
   JOIN case_analysis_sessions original ON original.id=parent.session_id
   JOIN case_analysis_sessions current_session ON current_session.id=NEW.session_id
   WHERE parent.id=NEW.revises_message_id AND parent.role='user' AND parent.response_status='completed'
     AND original.course_id=current_session.course_id AND original.student_id=current_session.student_id
     AND original.stage=current_session.stage AND original.is_practice=current_session.is_practice
 ) THEN
   RAISE EXCEPTION 'Invalid case chat revision parent' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER case_chat_revision_parent BEFORE INSERT ON case_analysis_messages FOR EACH ROW EXECUTE FUNCTION validate_case_chat_revision_parent();

CREATE OR REPLACE FUNCTION protect_case_chat_content() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF (NEW.id,NEW.session_id,NEW.role,NEW.content,NEW.sequence_number,NEW.in_reply_to,NEW.model_name,NEW.prompt_version,NEW.created_at,NEW.revises_message_id,NEW.performance_metrics)
    IS DISTINCT FROM (OLD.id,OLD.session_id,OLD.role,OLD.content,OLD.sequence_number,OLD.in_reply_to,OLD.model_name,OLD.prompt_version,OLD.created_at,OLD.revises_message_id,OLD.performance_metrics) THEN
   RAISE EXCEPTION 'Case chat message content and metadata are immutable';
 END IF;
 RETURN NEW;
END $$;
