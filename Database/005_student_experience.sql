ALTER TABLE submissions ADD COLUMN is_practice boolean NOT NULL DEFAULT false;
ALTER TABLE submissions ADD CONSTRAINT practice_is_individual CHECK(NOT is_practice OR student_user_id IS NOT NULL);
DROP INDEX uq_submission_individual_attempt;
CREATE UNIQUE INDEX uq_submission_individual_attempt ON submissions(assignment_id,student_user_id,attempt_no,is_practice) WHERE student_user_id IS NOT NULL;
CREATE INDEX ix_submission_official_assignment ON submissions(assignment_id,status) WHERE NOT is_practice;
