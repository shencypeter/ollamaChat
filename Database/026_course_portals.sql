ALTER TABLE assignments
 ADD COLUMN time_limit_minutes integer CHECK(time_limit_minutes BETWEEN 1 AND 1440),
 ADD COLUMN requires_start boolean NOT NULL DEFAULT false,
 ADD COLUMN notify_students boolean NOT NULL DEFAULT true;
ALTER TABLE assignment_questions ADD COLUMN section_title text NOT NULL DEFAULT '' CHECK(length(section_title)<=120);
ALTER TABLE report_assignments ADD COLUMN notify_students boolean NOT NULL DEFAULT true;
ALTER TABLE report_submissions ADD COLUMN published_score numeric(10,2), ADD COLUMN published_feedback text;
UPDATE report_submissions SET published_score=score,published_feedback=feedback WHERE score IS NOT NULL;
ALTER TABLE course_grade_scores ADD COLUMN published_score numeric(10,2);
UPDATE course_grade_scores SET published_score=score;
ALTER TABLE courses ADD COLUMN class_label text NOT NULL DEFAULT '', ADD COLUMN grade_adjustment numeric(8,2) NOT NULL DEFAULT 0;
CREATE TABLE course_final_grades (
 course_id uuid NOT NULL, student_id uuid NOT NULL, adjustment numeric(8,2) NOT NULL DEFAULT 0,
 published_total numeric(12,2), PRIMARY KEY(course_id,student_id),
 FOREIGN KEY(course_id,student_id) REFERENCES course_members(course_id,user_id) ON DELETE CASCADE
);
ALTER TABLE course_questions ADD COLUMN locked boolean NOT NULL DEFAULT false;
CREATE TABLE course_question_replies (
 id uuid PRIMARY KEY, question_id uuid NOT NULL REFERENCES course_questions(id) ON DELETE CASCADE,
 author_id uuid NOT NULL REFERENCES app_users(id), body text NOT NULL CHECK(length(trim(body)) BETWEEN 1 AND 8000),
 created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_question_replies ON course_question_replies(question_id,created_at,id);
