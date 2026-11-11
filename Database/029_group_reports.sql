ALTER TABLE course_groups ADD COLUMN dissolved_at timestamptz;
ALTER TABLE report_assignments
 ADD COLUMN submission_mode text NOT NULL DEFAULT 'individual' CHECK(submission_mode IN ('individual','group')),
 ADD COLUMN allow_delegate boolean NOT NULL DEFAULT false,
 ADD COLUMN allow_late boolean NOT NULL DEFAULT false,
 ADD COLUMN evaluation_mode text NOT NULL DEFAULT 'off' CHECK(evaluation_mode IN ('off','simple','consensus')),
 ADD COLUMN dimensions text[] NOT NULL DEFAULT ARRAY['工作參與','任務完成','合作與溝通','整體貢獻'],
 ADD UNIQUE(id,course_id);
CREATE TABLE report_groups(
 report_id uuid NOT NULL, course_id uuid NOT NULL, group_id uuid NOT NULL,
 PRIMARY KEY(report_id,group_id),
 FOREIGN KEY(report_id,course_id) REFERENCES report_assignments(id,course_id) ON DELETE CASCADE,
 FOREIGN KEY(group_id,course_id) REFERENCES course_groups(id,course_id)
);
ALTER TABLE report_submissions ADD COLUMN group_id uuid, ADD COLUMN group_name text,
 ADD COLUMN revision integer NOT NULL DEFAULT 1, ADD COLUMN returned boolean NOT NULL DEFAULT false,
 ADD FOREIGN KEY(report_id,group_id) REFERENCES report_groups(report_id,group_id),
 ADD UNIQUE(report_id,group_id), ADD UNIQUE(id,report_id);
CREATE TABLE report_snapshot(
 submission_id uuid NOT NULL, report_id uuid NOT NULL, student_id uuid NOT NULL REFERENCES app_users,
 display_name text NOT NULL, is_leader boolean NOT NULL,
 override_score numeric(10,2), reason text NOT NULL DEFAULT '', published_score numeric(10,2),
 PRIMARY KEY(submission_id,student_id), UNIQUE(report_id,student_id),
 FOREIGN KEY(submission_id,report_id) REFERENCES report_submissions(id,report_id) ON DELETE CASCADE
);
CREATE TABLE report_previous_files(
 submission_id uuid REFERENCES report_submissions ON DELETE CASCADE, revision integer,
 actor uuid NOT NULL REFERENCES app_users, submitted_at timestamptz NOT NULL, file_name text NOT NULL, content bytea NOT NULL,
 PRIMARY KEY(submission_id,revision)
);
CREATE TABLE report_peer_reviews(
 submission_id uuid, reviewer uuid, subject uuid, ratings integer[] NOT NULL, comment varchar(500) NOT NULL,
 PRIMARY KEY(submission_id,reviewer,subject), CHECK(reviewer<>subject AND 1<=ALL(ratings) AND 5>=ALL(ratings)),
 FOREIGN KEY(submission_id,reviewer) REFERENCES report_snapshot ON DELETE CASCADE,
 FOREIGN KEY(submission_id,subject) REFERENCES report_snapshot ON DELETE CASCADE
);
CREATE TABLE report_votes(
 submission_id uuid, voter uuid, choice text NOT NULL CHECK(choice IN ('agree','review','pending')), explanation varchar(500) NOT NULL,
 PRIMARY KEY(submission_id,voter), FOREIGN KEY(submission_id,voter) REFERENCES report_snapshot ON DELETE CASCADE
);
CREATE TABLE report_grade_events(
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, submission_id uuid REFERENCES report_submissions ON DELETE CASCADE,
 actor uuid NOT NULL REFERENCES app_users, created_at timestamptz NOT NULL DEFAULT now(), details jsonb NOT NULL
);

INSERT INTO report_grade_events(submission_id,actor,created_at,details)
 SELECT id,graded_by,COALESCE(graded_at,submitted_at),
        jsonb_build_object('migration',29,'score',score,'feedback',feedback,'publishedScore',published_score,'publishedFeedback',published_feedback)
 FROM report_submissions WHERE graded_by IS NOT NULL AND (score IS NOT NULL OR published_score IS NOT NULL);

CREATE VIEW report_group_audience AS
 SELECT rg.report_id,gm.user_id AS student_id FROM report_groups rg
 JOIN course_group_members gm ON gm.group_id=rg.group_id AND gm.left_at IS NULL
 UNION SELECT report_id,student_id FROM report_snapshot;
