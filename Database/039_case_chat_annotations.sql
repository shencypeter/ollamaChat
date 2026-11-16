-- Optional metadata for the immutable answer referenced by in_reply_to.
-- Existing history remains unannotated; never infer metadata for old responses.
ALTER TABLE case_analysis_messages ADD COLUMN annotations jsonb,
 ADD CONSTRAINT case_chat_annotations_role CHECK(annotations IS NULL OR
   (role='assistant' AND jsonb_typeof(annotations)='array' AND jsonb_array_length(annotations)<=48));

CREATE OR REPLACE FUNCTION protect_case_chat_content() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF (NEW.id,NEW.session_id,NEW.role,NEW.content,NEW.sequence_number,NEW.in_reply_to,NEW.model_name,NEW.prompt_version,NEW.created_at,NEW.revises_message_id,NEW.performance_metrics,NEW.annotations)
    IS DISTINCT FROM (OLD.id,OLD.session_id,OLD.role,OLD.content,OLD.sequence_number,OLD.in_reply_to,OLD.model_name,OLD.prompt_version,OLD.created_at,OLD.revises_message_id,OLD.performance_metrics,OLD.annotations) THEN
   RAISE EXCEPTION 'Case chat message content and metadata are immutable';
 END IF;
 RETURN NEW;
END $$;
