ALTER TABLE course_resources ADD COLUMN notify_students boolean NOT NULL DEFAULT false;
ALTER TABLE notification_reads DROP CONSTRAINT notification_reads_kind_check;
ALTER TABLE notification_reads ADD CHECK(kind IN ('quiz','report','question','material'));
ALTER TABLE course_group_members ADD COLUMN is_leader boolean NOT NULL DEFAULT false;
CREATE UNIQUE INDEX uq_current_group_leader ON course_group_members(group_id) WHERE is_leader AND left_at IS NULL;
CREATE TABLE course_question_votes (
 question_id uuid NOT NULL REFERENCES course_questions(id) ON DELETE CASCADE,
 user_id uuid NOT NULL REFERENCES app_users(id), PRIMARY KEY(question_id,user_id)
);
ALTER TABLE course_questions ADD COLUMN attachment_name text, ADD COLUMN attachment_bytes bytea;
ALTER TABLE course_members ADD COLUMN ta_restrictions text[] NOT NULL DEFAULT '{}'
 CHECK(ta_restrictions <@ ARRAY['materials','assessments','grading','statistics']::text[]);
