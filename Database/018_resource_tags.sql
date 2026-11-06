ALTER TABLE course_resources
 ADD COLUMN tags text[] NOT NULL DEFAULT '{}',
 ADD COLUMN tags_version integer NOT NULL DEFAULT 1 CHECK(tags_version>0),
 ADD CONSTRAINT ck_resource_tags CHECK(cardinality(tags)<=10 AND array_position(tags,NULL) IS NULL);
