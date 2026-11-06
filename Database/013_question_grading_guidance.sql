ALTER TABLE assignment_questions ADD COLUMN grading_guidance text NOT NULL DEFAULT '' CHECK(length(grading_guidance)<=8000);
