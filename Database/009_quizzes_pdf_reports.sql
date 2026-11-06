CREATE TABLE report_assignments (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),course_id uuid NOT NULL REFERENCES courses(id),
 title text NOT NULL,instructions text NOT NULL DEFAULT '',max_score numeric(10,2) NOT NULL CHECK(max_score>0),
 opens_at timestamptz,due_at timestamptz,status text NOT NULL DEFAULT 'draft' CHECK(status IN ('draft','published','closed')),
 version integer NOT NULL DEFAULT 1,created_by uuid NOT NULL REFERENCES app_users(id),created_at timestamptz NOT NULL DEFAULT now(),
 CHECK(due_at IS NULL OR opens_at IS NULL OR due_at>opens_at)
);
CREATE TABLE report_submissions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),report_id uuid NOT NULL REFERENCES report_assignments(id),
 student_id uuid NOT NULL REFERENCES app_users(id),file_name text NOT NULL,content bytea NOT NULL,
 submitted_at timestamptz NOT NULL DEFAULT now(),score numeric(10,2),feedback text NOT NULL DEFAULT '',
 graded_by uuid REFERENCES app_users(id),graded_at timestamptz,version integer NOT NULL DEFAULT 1,
 UNIQUE(report_id,student_id),CHECK(score IS NULL OR score>=0)
);
ALTER TABLE course_grade_items ADD COLUMN source_assignment_id uuid REFERENCES assignments(id);
ALTER TABLE course_grade_items ADD COLUMN source_report_id uuid REFERENCES report_assignments(id);
ALTER TABLE course_grade_items ADD CHECK(source_assignment_id IS NULL OR source_report_id IS NULL);
CREATE UNIQUE INDEX uq_grade_item_quiz ON course_grade_items(source_assignment_id) WHERE source_assignment_id IS NOT NULL;
CREATE UNIQUE INDEX uq_grade_item_report ON course_grade_items(source_report_id) WHERE source_report_id IS NOT NULL;
CREATE INDEX ix_reports_course ON report_assignments(course_id);
CREATE INDEX ix_report_submissions_student ON report_submissions(student_id);
ALTER TABLE course_grade_items DROP CONSTRAINT course_grade_items_category_check;
ALTER TABLE course_grade_items ADD CHECK(category IN ('offline_assignment','assignment','quiz','midterm','final','other'));
