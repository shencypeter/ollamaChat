ALTER TABLE report_submissions ADD COLUMN is_practice boolean NOT NULL DEFAULT false;
ALTER TABLE report_submissions DROP CONSTRAINT report_submissions_report_id_student_id_key;
ALTER TABLE report_submissions ADD CONSTRAINT uq_report_student_mode UNIQUE(report_id,student_id,is_practice);
CREATE INDEX ix_practice_reports ON report_submissions(student_id,report_id) WHERE is_practice;
