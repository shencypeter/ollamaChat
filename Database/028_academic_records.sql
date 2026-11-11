-- Preserve institutional codes from existing integrations; only parse canonical term codes.
ALTER TABLE courses
 ADD COLUMN academic_year integer GENERATED ALWAYS AS
 (CASE WHEN term_code ~ '^[0-9]{1,3}-(1|2|S)$' THEN split_part(term_code,'-',1)::integer END) STORED,
 ADD COLUMN semester text GENERATED ALWAYS AS
 (CASE WHEN term_code ~ '^[0-9]{1,3}-(1|2|S)$' THEN split_part(term_code,'-',2) END) STORED;
ALTER TABLE app_users ADD COLUMN department text, ADD COLUMN grade_level text;
