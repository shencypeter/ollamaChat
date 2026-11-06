CREATE TABLE course_grade_items (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 course_id uuid NOT NULL REFERENCES courses(id),
 title text NOT NULL CHECK(length(trim(title)) BETWEEN 1 AND 200),
 category text NOT NULL CHECK(category IN ('offline_assignment','quiz','midterm','final','other')),
 max_score numeric(10,2) NOT NULL CHECK(max_score > 0),
 weight numeric(10,3) NOT NULL CHECK(weight > 0),
 version integer NOT NULL DEFAULT 1,
 created_by uuid NOT NULL REFERENCES app_users(id),
 created_at timestamptz NOT NULL DEFAULT now(), UNIQUE(id,course_id)
);
CREATE TABLE course_grade_scores (
 item_id uuid NOT NULL, course_id uuid NOT NULL,
 student_id uuid NOT NULL REFERENCES app_users(id),
 score numeric(10,2) CHECK(score >= 0),
 version integer NOT NULL DEFAULT 1,
 updated_by uuid NOT NULL REFERENCES app_users(id),
 updated_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(item_id,student_id),
 FOREIGN KEY(item_id,course_id) REFERENCES course_grade_items(id,course_id)
);
CREATE INDEX ix_grade_items_course ON course_grade_items(course_id);
CREATE INDEX ix_grade_scores_course_student ON course_grade_scores(course_id,student_id);
