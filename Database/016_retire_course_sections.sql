-- Fail closed if a live deployment has not completed its course mapping.
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM course_sections) OR EXISTS(SELECT 1 FROM course_resources WHERE section_id IS NOT NULL) THEN
  RAISE EXCEPTION 'Course section mappings must be completed before retirement';
 END IF;
 IF EXISTS(SELECT 1 FROM course_groups GROUP BY course_id,name HAVING count(*)>1) THEN
  RAISE EXCEPTION 'Resolve duplicate course group names before retirement';
 END IF;
 IF EXISTS(SELECT 1 FROM course_group_members gm JOIN course_groups g ON g.id=gm.group_id WHERE gm.left_at IS NULL GROUP BY g.course_id,gm.user_id HAVING count(*)>1) THEN
  RAISE EXCEPTION 'Resolve duplicate active course group memberships before retirement';
 END IF;
END $$;
DROP INDEX ux_groups_section_name;
ALTER TABLE course_members DROP COLUMN section_id;
ALTER TABLE course_groups DROP COLUMN section_id;
ALTER TABLE course_resources DROP COLUMN section_id;
DROP TABLE course_sections;
ALTER TABLE course_groups ADD CONSTRAINT course_groups_course_id_name_key UNIQUE(course_id,name);
