ALTER TABLE courses ADD COLUMN case_analysis_enabled boolean NOT NULL DEFAULT false;

CREATE TABLE case_analysis_sessions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 course_id uuid NOT NULL REFERENCES courses(id),
 student_id uuid NOT NULL REFERENCES app_users(id),
 stage text NOT NULL CHECK(stage IN ('premorbid','illnessCourse','functionalAssessment','psychosocial')),
 status text NOT NULL DEFAULT 'active' CHECK(status IN ('active','closed')),
 is_practice boolean NOT NULL DEFAULT false,
 started_at timestamptz NOT NULL DEFAULT now(),
 last_activity_at timestamptz NOT NULL DEFAULT now(),
 completed_at timestamptz,
 next_sequence integer NOT NULL DEFAULT 1 CHECK(next_sequence>0)
);
CREATE UNIQUE INDEX ix_case_chat_active ON case_analysis_sessions(course_id,student_id,stage,is_practice) WHERE status='active';
CREATE INDEX ix_case_chat_roster ON case_analysis_sessions(course_id,student_id,stage) WHERE NOT is_practice;

CREATE TABLE case_analysis_messages (
 id uuid PRIMARY KEY,
 session_id uuid NOT NULL REFERENCES case_analysis_sessions(id),
 role text NOT NULL CHECK(role IN ('user','assistant')),
 content text NOT NULL CHECK(length(btrim(content)) BETWEEN 1 AND 16000),
 sequence_number integer NOT NULL CHECK(sequence_number>0),
 response_status text NOT NULL CHECK(response_status IN ('pending','completed','failed')),
 in_reply_to uuid,
 model_name text NOT NULL,
 prompt_version text NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(),
 attempt_token uuid,
 lease_until timestamptz,
 failure_code text CHECK(failure_code IN ('provider_failed','context_limit','access_changed','expired')),
 UNIQUE(session_id,sequence_number), UNIQUE(session_id,id), UNIQUE(in_reply_to),
 FOREIGN KEY(session_id,in_reply_to) REFERENCES case_analysis_messages(session_id,id),
 CHECK((role='user' AND in_reply_to IS NULL) OR (role='assistant' AND in_reply_to IS NOT NULL AND response_status='completed'))
);
CREATE UNIQUE INDEX ix_case_chat_pending ON case_analysis_messages(session_id) WHERE response_status='pending';

-- Content is a historical record. Retry may update only status/attempt metadata.
CREATE FUNCTION protect_case_chat_content() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF (NEW.id,NEW.session_id,NEW.role,NEW.content,NEW.sequence_number,NEW.in_reply_to,NEW.model_name,NEW.prompt_version,NEW.created_at)
    IS DISTINCT FROM (OLD.id,OLD.session_id,OLD.role,OLD.content,OLD.sequence_number,OLD.in_reply_to,OLD.model_name,OLD.prompt_version,OLD.created_at) THEN
   RAISE EXCEPTION 'Case chat message content is immutable';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER case_chat_content_immutable BEFORE UPDATE ON case_analysis_messages FOR EACH ROW EXECUTE FUNCTION protect_case_chat_content();
