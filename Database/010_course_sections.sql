CREATE TABLE course_sections (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), course_id uuid NOT NULL REFERENCES courses(id),
 name text NOT NULL CHECK(length(btrim(name)) BETWEEN 1 AND 100), schedule text NOT NULL DEFAULT '' CHECK(length(schedule)<=300),
 UNIQUE(course_id,name), UNIQUE(id,course_id)
);
ALTER TABLE course_members ADD COLUMN section_id uuid;
ALTER TABLE course_members ADD CONSTRAINT fk_member_section FOREIGN KEY(section_id,course_id) REFERENCES course_sections(id,course_id);
ALTER TABLE course_members ADD CONSTRAINT ck_student_section CHECK(section_id IS NULL OR course_role='student');
ALTER TABLE course_groups ADD COLUMN section_id uuid;
ALTER TABLE course_groups ADD CONSTRAINT fk_group_section FOREIGN KEY(section_id,course_id) REFERENCES course_sections(id,course_id);
ALTER TABLE course_groups DROP CONSTRAINT course_groups_course_id_name_key;
CREATE UNIQUE INDEX ux_groups_section_name ON course_groups(course_id,section_id,name) NULLS NOT DISTINCT;
ALTER TABLE course_resources ADD COLUMN section_id uuid;
ALTER TABLE course_resources ADD CONSTRAINT fk_resource_section FOREIGN KEY(section_id,course_id) REFERENCES course_sections(id,course_id);
CREATE INDEX ix_members_section ON course_members(section_id);
-- Existing students/groups remain unassigned; existing resources remain shared.
