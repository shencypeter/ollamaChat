ALTER TABLE courses
 ADD COLUMN version integer NOT NULL DEFAULT 1 CHECK(version>0),
 ADD COLUMN updated_at timestamptz NOT NULL DEFAULT now();

CREATE TABLE course_units (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 course_id uuid NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
 name varchar(160) NOT NULL CHECK(length(btrim(name)) BETWEEN 1 AND 160),
 description varchar(8000) NOT NULL DEFAULT '',
 sort_order integer NOT NULL CHECK(sort_order>0),
 version integer NOT NULL DEFAULT 1 CHECK(version>0),
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(id,course_id),
 UNIQUE(course_id,sort_order) DEFERRABLE INITIALLY DEFERRED
);

ALTER TABLE course_resources ADD COLUMN unit_id uuid;
ALTER TABLE course_resources ADD CONSTRAINT fk_resource_unit
 FOREIGN KEY(unit_id,course_id) REFERENCES course_units(id,course_id);
ALTER TABLE assignments ADD COLUMN unit_id uuid;
ALTER TABLE assignments ADD CONSTRAINT fk_assignment_unit
 FOREIGN KEY(unit_id,course_id) REFERENCES course_units(id,course_id);
ALTER TABLE report_assignments ADD COLUMN unit_id uuid;
ALTER TABLE report_assignments ADD CONSTRAINT fk_report_unit
 FOREIGN KEY(unit_id,course_id) REFERENCES course_units(id,course_id);
ALTER TABLE exams ADD COLUMN unit_id uuid;
ALTER TABLE exams ADD CONSTRAINT fk_exam_unit
 FOREIGN KEY(unit_id,course_id) REFERENCES course_units(id,course_id);

INSERT INTO course_units(id,course_id,name,description,sort_order)
SELECT gen_random_uuid(),c.id,'既有教學活動','由系統在單元架構升級時建立；教師可重新命名、補充說明及調整內容。',1
FROM courses c
WHERE EXISTS(SELECT 1 FROM course_resources r WHERE r.course_id=c.id)
   OR EXISTS(SELECT 1 FROM assignments a WHERE a.course_id=c.id)
   OR EXISTS(SELECT 1 FROM report_assignments a WHERE a.course_id=c.id)
   OR EXISTS(SELECT 1 FROM exams e WHERE e.course_id=c.id);

UPDATE course_resources r SET unit_id=u.id FROM course_units u
 WHERE u.course_id=r.course_id AND u.sort_order=1 AND r.unit_id IS NULL;
UPDATE assignments a SET unit_id=u.id FROM course_units u
 WHERE u.course_id=a.course_id AND u.sort_order=1 AND a.unit_id IS NULL;
UPDATE report_assignments a SET unit_id=u.id FROM course_units u
 WHERE u.course_id=a.course_id AND u.sort_order=1 AND a.unit_id IS NULL;
UPDATE exams e SET unit_id=u.id FROM course_units u
 WHERE u.course_id=e.course_id AND u.sort_order=1 AND e.unit_id IS NULL;

CREATE INDEX ix_course_units_course_sort ON course_units(course_id,sort_order);
CREATE INDEX ix_resources_unit ON course_resources(unit_id) WHERE unit_id IS NOT NULL;
CREATE INDEX ix_assignments_unit ON assignments(unit_id) WHERE unit_id IS NOT NULL;
CREATE INDEX ix_reports_unit ON report_assignments(unit_id) WHERE unit_id IS NOT NULL;
CREATE INDEX ix_exams_unit ON exams(unit_id) WHERE unit_id IS NOT NULL;
