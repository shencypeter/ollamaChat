ALTER TABLE course_questions
 DROP CONSTRAINT ck_course_questions_title;

ALTER TABLE course_questions
 ADD CONSTRAINT ck_course_questions_title
 CHECK(title = '' OR length(btrim(title)) BETWEEN 1 AND 200);
