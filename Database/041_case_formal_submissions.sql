-- AI analysis messages remain private working history. Formal snapshots are explicit opt-in.
-- No historical draft is automatically submitted or rewritten.
CREATE TABLE case_formal_submissions (
 id uuid PRIMARY KEY,
 course_id uuid NOT NULL REFERENCES courses(id),
 student_id uuid NOT NULL REFERENCES app_users(id),
 stage text NOT NULL CHECK(stage IN ('premorbid','illnessCourse','functionalAssessment','psychosocial')),
 version integer NOT NULL CHECK(version>0),
 status text NOT NULL DEFAULT 'submitted' CHECK(status IN ('submitted','returned')),
 assignment_id uuid,
 analysis_message_id uuid NOT NULL REFERENCES case_analysis_messages(id),
 answer_text text NOT NULL,
 feedback_text text NOT NULL,
 annotations jsonb NOT NULL DEFAULT '[]'::jsonb CHECK(jsonb_typeof(annotations)='array'),
 model_name text NOT NULL,
 prompt_version text NOT NULL,
 rule_version integer,
 rule_snapshot jsonb,
 submitted_at timestamptz NOT NULL,
 returned_at timestamptz,
 returned_by uuid REFERENCES app_users(id),
 return_reason text CHECK(return_reason IS NULL OR length(btrim(return_reason)) BETWEEN 1 AND 2000),
 UNIQUE(course_id,student_id,stage,version),
 FOREIGN KEY(assignment_id,course_id) REFERENCES assignments(id,course_id),
 CHECK((status='submitted' AND returned_at IS NULL AND returned_by IS NULL AND return_reason IS NULL)
    OR (status='returned' AND returned_at IS NOT NULL AND returned_by IS NOT NULL AND return_reason IS NOT NULL))
);
CREATE INDEX ix_case_formal_review ON case_formal_submissions(course_id,submitted_at DESC);
CREATE FUNCTION protect_case_formal_snapshot() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF (NEW.id,NEW.course_id,NEW.student_id,NEW.stage,NEW.version,NEW.assignment_id,NEW.analysis_message_id,
     NEW.answer_text,NEW.feedback_text,NEW.annotations,NEW.model_name,NEW.prompt_version,NEW.submitted_at,NEW.rule_version,NEW.rule_snapshot)
    IS DISTINCT FROM
    (OLD.id,OLD.course_id,OLD.student_id,OLD.stage,OLD.version,OLD.assignment_id,OLD.analysis_message_id,
     OLD.answer_text,OLD.feedback_text,OLD.annotations,OLD.model_name,OLD.prompt_version,OLD.submitted_at,OLD.rule_version,OLD.rule_snapshot)
    OR OLD.status='returned' THEN
   RAISE EXCEPTION 'Formal submission snapshots are immutable';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER case_formal_snapshot_immutable BEFORE UPDATE ON case_formal_submissions
 FOR EACH ROW EXECUTE FUNCTION protect_case_formal_snapshot();
