ALTER TABLE course_resources
 ADD COLUMN teaching_date date,
 ADD COLUMN summary text NOT NULL DEFAULT '' CHECK(length(summary)<=10000),
 ADD COLUMN metadata_version integer NOT NULL DEFAULT 1 CHECK(metadata_version>0);
CREATE INDEX ix_resource_teaching_date ON course_resources(course_id,teaching_date,created_at,id);
