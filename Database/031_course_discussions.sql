ALTER TABLE course_questions
 ADD COLUMN title text NOT NULL DEFAULT '';

UPDATE course_questions
SET title = left(btrim(question), 200)
WHERE title = '';

ALTER TABLE course_questions
 ADD CONSTRAINT ck_course_questions_title
 CHECK(length(btrim(title)) BETWEEN 1 AND 200);

CREATE TABLE course_question_reply_likes (
 reply_id uuid NOT NULL REFERENCES course_question_replies(id) ON DELETE CASCADE,
 user_id uuid NOT NULL REFERENCES app_users(id),
 created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(reply_id,user_id)
);
