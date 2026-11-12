ALTER TABLE course_resources ADD COLUMN unit_sort_order integer NOT NULL DEFAULT 0 CHECK(unit_sort_order >= 0);
ALTER TABLE assignments ADD COLUMN unit_sort_order integer NOT NULL DEFAULT 0 CHECK(unit_sort_order >= 0);
ALTER TABLE report_assignments ADD COLUMN unit_sort_order integer NOT NULL DEFAULT 0 CHECK(unit_sort_order >= 0);

WITH combined AS (
  SELECT 'material' kind,id,unit_id,created_at FROM course_resources WHERE unit_id IS NOT NULL
  UNION ALL SELECT 'assignment',id,unit_id,created_at FROM assignments WHERE unit_id IS NOT NULL
  UNION ALL SELECT 'report',id,unit_id,created_at FROM report_assignments WHERE unit_id IS NOT NULL
), ranked AS (
  SELECT kind,id,row_number() OVER(PARTITION BY unit_id ORDER BY created_at,id)::integer position FROM combined
)
UPDATE course_resources target SET unit_sort_order=ranked.position FROM ranked WHERE ranked.kind='material' AND ranked.id=target.id;

WITH combined AS (
  SELECT 'material' kind,id,unit_id,created_at FROM course_resources WHERE unit_id IS NOT NULL
  UNION ALL SELECT 'assignment',id,unit_id,created_at FROM assignments WHERE unit_id IS NOT NULL
  UNION ALL SELECT 'report',id,unit_id,created_at FROM report_assignments WHERE unit_id IS NOT NULL
), ranked AS (
  SELECT kind,id,row_number() OVER(PARTITION BY unit_id ORDER BY created_at,id)::integer position FROM combined
)
UPDATE assignments target SET unit_sort_order=ranked.position FROM ranked WHERE ranked.kind='assignment' AND ranked.id=target.id;

WITH combined AS (
  SELECT 'material' kind,id,unit_id,created_at FROM course_resources WHERE unit_id IS NOT NULL
  UNION ALL SELECT 'assignment',id,unit_id,created_at FROM assignments WHERE unit_id IS NOT NULL
  UNION ALL SELECT 'report',id,unit_id,created_at FROM report_assignments WHERE unit_id IS NOT NULL
), ranked AS (
  SELECT kind,id,row_number() OVER(PARTITION BY unit_id ORDER BY created_at,id)::integer position FROM combined
)
UPDATE report_assignments target SET unit_sort_order=ranked.position FROM ranked WHERE ranked.kind='report' AND ranked.id=target.id;

UPDATE exams SET unit_id=NULL WHERE unit_id IS NOT NULL;

CREATE INDEX ix_resources_unit_order ON course_resources(course_id,unit_id,unit_sort_order) WHERE unit_id IS NOT NULL;
CREATE INDEX ix_assignments_unit_order ON assignments(course_id,unit_id,unit_sort_order) WHERE unit_id IS NOT NULL;
CREATE INDEX ix_reports_unit_order ON report_assignments(course_id,unit_id,unit_sort_order) WHERE unit_id IS NOT NULL;
